using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Firemka.Application.Charging;

namespace Firemka.Infrastructure.Charging;

public sealed class ChargingCsvParser : IChargingCsvParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public async Task<ChargingCsvParseResult> ParseAsync(
        Stream content,
        ChargingProfileSnapshot profile,
        ChargingImportLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(limits);
        if (!profile.IsActive || !profile.WhConfirmed)
        {
            throw new InvalidOperationException("Profil CSV musi być aktywny i mieć potwierdzoną jednostkę Wh.");
        }

        if (limits.MaxBytes <= 0 || limits.MaxRows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }

        var bytes = await ReadBoundedAsync(content, limits.MaxBytes, cancellationToken);
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ChargingCsvValidationException("Plik nie jest poprawnym UTF-8.", null) { Source = exception.Source };
        }

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var records = ParseRecords(text, profile.Separator);
        if (records.Count == 0)
        {
            throw new ChargingCsvValidationException("Plik nie zawiera nagłówka.");
        }

        var header = records[0].Fields;
        if (header.Count != header.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ChargingCsvValidationException("Nagłówek zawiera powtórzoną nazwę kolumny.", 1);
        }

        var timestampIndex = FindRequiredColumn(header, profile.TimestampColumn);
        var energyIndex = FindRequiredColumn(header, profile.EnergyWhColumn);
        var identityIndex = profile.IdentityColumn is null
            ? (int?)null
            : FindRequiredColumn(header, profile.IdentityColumn);
        var dataRecords = records.Skip(1).ToArray();
        if (dataRecords.Length > limits.MaxRows)
        {
            throw new ChargingCsvValidationException($"Plik przekracza limit {limits.MaxRows} wierszy.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId);
        var parsed = new List<ParsedChargingRow>(dataRecords.Length);
        foreach (var record in dataRecords)
        {
            if (record.Fields.Count != header.Count)
            {
                throw new ChargingCsvValidationException(
                    $"Liczba pól ({record.Fields.Count}) różni się od nagłówka ({header.Count}).",
                    record.RowNumber);
            }

            var originalTimestamp = record.Fields[timestampIndex].Trim();
            if (!DateTime.TryParseExact(
                    originalTimestamp,
                    profile.DateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var localDateTime))
            {
                throw new ChargingCsvValidationException(
                    $"Nie można odczytać daty według formatu {profile.DateFormat}.",
                    record.RowNumber);
            }

            localDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
            if (timeZone.IsInvalidTime(localDateTime))
            {
                throw new ChargingCsvValidationException("Czas nie istnieje z powodu zmiany czasu.", record.RowNumber);
            }

            if (timeZone.IsAmbiguousTime(localDateTime))
            {
                throw new ChargingCsvValidationException("Czas jest niejednoznaczny z powodu zmiany czasu.", record.RowNumber);
            }

            var energyText = record.Fields[energyIndex].Trim();
            if (!decimal.TryParse(
                    energyText,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    CultureInfo.InvariantCulture,
                    out var energyWh)
                || energyWh <= 0)
            {
                throw new ChargingCsvValidationException("Energia Wh musi być liczbą większą od zera.", record.RowNumber);
            }

            var identity = identityIndex is int index
                ? NormalizeOptional(record.Fields[index])
                : null;
            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone), TimeSpan.Zero);
            var localMonth = new DateOnly(localDateTime.Year, localDateTime.Month, 1);
            var rowHash = HashRow(utc, energyWh, identity);
            parsed.Add(new ParsedChargingRow(
                record.RowNumber,
                utc,
                localMonth,
                originalTimestamp,
                energyWh,
                identity,
                rowHash));
        }

        return new ChargingCsvParseResult(
            Convert.ToHexString(SHA256.HashData(bytes)),
            parsed);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > maxBytes)
            {
                throw new ChargingCsvValidationException($"Plik przekracza limit {maxBytes} bajtów.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return destination.ToArray();
    }

    private static List<CsvRecord> ParseRecords(string text, char separator)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var afterClosingQuote = false;
        var atFieldStart = true;

        void FinishField()
        {
            fields.Add(field.ToString());
            field.Clear();
            afterClosingQuote = false;
            atFieldStart = true;
        }

        void FinishRecord()
        {
            FinishField();
            if (fields.Count != 1 || fields[0].Length != 0)
            {
                records.Add(new CsvRecord(records.Count + 1, fields.ToArray()));
            }

            fields.Clear();
        }

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterClosingQuote = true;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (afterClosingQuote && current != separator && current is not '\r' and not '\n')
            {
                throw new ChargingCsvValidationException("Po zamknięciu cudzysłowu znaleziono niedozwolony znak.", records.Count + 1);
            }

            if (current == '"')
            {
                if (!atFieldStart)
                {
                    throw new ChargingCsvValidationException("Cudzysłów może rozpoczynać tylko całe pole.", records.Count + 1);
                }

                inQuotes = true;
                atFieldStart = false;
            }
            else if (current == separator)
            {
                FinishField();
            }
            else if (current is '\r' or '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                FinishRecord();
            }
            else
            {
                field.Append(current);
                atFieldStart = false;
            }
        }

        if (inQuotes)
        {
            throw new ChargingCsvValidationException("Pole cytowane nie ma zamykającego cudzysłowu.", records.Count + 1);
        }

        if (field.Length > 0 || fields.Count > 0 || afterClosingQuote)
        {
            FinishRecord();
        }

        return records;
    }

    private static int FindRequiredColumn(IReadOnlyList<string> header, string name)
    {
        for (var index = 0; index < header.Count; index++)
        {
            if (string.Equals(header[index], name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new ChargingCsvValidationException($"Brakuje wymaganej kolumny „{name}”.", 1);
    }

    private static string? NormalizeOptional(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HashRow(DateTimeOffset utc, decimal energyWh, string? identity)
    {
        var canonical = string.Join(
            '\u001F',
            utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            energyWh.ToString("G29", CultureInfo.InvariantCulture),
            identity ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record CsvRecord(int RowNumber, IReadOnlyList<string> Fields);
}
