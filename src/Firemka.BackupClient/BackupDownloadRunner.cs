using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Firemka.Application.Backups;

namespace Firemka.BackupClient;

public sealed class BackupDownloadRunner(HttpClient httpClient, IBackupSecretReader secrets)
{
    public async Task<IReadOnlyList<SavedBackup>> RunAsync(
        BackupClientConfiguration configuration,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ServerUrl.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Klient kopii wymaga HTTPS.");
        if (httpClient.BaseAddress is null)
            httpClient.BaseAddress = configuration.ServerUrl;
        else if (httpClient.BaseAddress != configuration.ServerUrl)
            throw new InvalidOperationException("Klient HTTP jest już przypisany do innego serwera Firemki.");
        var token = await secrets.ReadTokenAsync(cancellationToken);
        var recoveryPassword = await secrets.ReadRecoveryPasswordAsync(cancellationToken);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await FlushPendingReportsAsync(
            configuration.BackupFolder, recoveryPassword, cancellationToken);
        using var planResponse = await httpClient.GetAsync("/api/backups/plan", cancellationToken);
        planResponse.EnsureSuccessStatusCode();
        var plan = await planResponse.Content.ReadFromJsonAsync<BackupPlan>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new InvalidDataException("Serwer zwrócił pusty plan kopii.");
        var saved = new List<SavedBackup>();
        if (plan.DailyRequired)
            saved.Add(await DownloadOneAsync(configuration, recoveryPassword, nowUtc,
                plan.ManualRequestId, null, cancellationToken));
        foreach (var annual in plan.AnnualArchives)
            saved.Add(await DownloadOneAsync(configuration, recoveryPassword, nowUtc,
                null, annual, cancellationToken));
        return saved;
    }

    private async Task<SavedBackup> DownloadOneAsync(
        BackupClientConfiguration configuration,
        string recoveryPassword,
        DateTimeOffset nowUtc,
        Guid? manualRequestId,
        AnnualBackupPlan? annual,
        CancellationToken cancellationToken)
    {
        SavedBackup? saved = null;
        string? journalPath = null;
        try
        {
            using var response = await httpClient.GetAsync(
                "/api/backups/export", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var payload = await response.Content.ReadAsStreamAsync(cancellationToken);
            saved = await BackupFileManager.SaveVerifiedAsync(
                payload, configuration.BackupFolder, recoveryPassword, nowUtc,
                annual?.TaxYear, cancellationToken);
            var report = new BackupReportCommand(
                true, saved.FileName, saved.ManifestSha256, saved.FormatVersion, null,
                manualRequestId, annual?.RequestId);
            try
            {
                journalPath = await BackupReportJournal.SaveAsync(
                    configuration.BackupFolder, saved, report, cancellationToken);
            }
            catch
            {
                await TryReportFailureAsync("local-state-failed", cancellationToken);
                throw;
            }
            await ReportAsync(report, cancellationToken);
            BackupReportJournal.Delete(journalPath);
            return saved;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (saved is null)
                await TryReportFailureAsync(FailureCode(exception), cancellationToken);
            throw;
        }
    }

    private async Task FlushPendingReportsAsync(
        string rootFolder,
        string recoveryPassword,
        CancellationToken cancellationToken)
    {
        foreach (var journalPath in BackupReportJournal.Enumerate(rootFolder).Order(StringComparer.Ordinal))
        {
            var pending = await BackupReportJournal.ReadAndVerifyAsync(
                rootFolder, journalPath, recoveryPassword, cancellationToken);
            await ReportAsync(pending.Report, cancellationToken);
            BackupReportJournal.Delete(journalPath);
        }
    }

    private async Task ReportAsync(BackupReportCommand command, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/backups/report", command,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task TryReportFailureAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            await ReportAsync(new BackupReportCommand(false, null, null, null, code, null, null), cancellationToken);
        }
        catch
        {
        }
    }

    private static string FailureCode(Exception exception) => exception switch
    {
        InvalidDataException => "verification-failed",
        IOException => "local-write-failed",
        HttpRequestException => "download-failed",
        _ => "client-failure",
    };
}
