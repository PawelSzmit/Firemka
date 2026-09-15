using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Firemka.BackupClient;

public static class FmbakEncryption
{
    private static readonly byte[] Magic = "FMBACK01"u8.ToArray();
    private const int FormatVersion = 1;
    private const int Iterations = 600_000;
    private const int ChunkSize = 1024 * 1024;
    private const int SaltSize = 16;
    private const int NoncePrefixSize = 8;
    private const int TagSize = 16;
    private const int HeaderSize = 8 + 4 + 4 + 4 + SaltSize + NoncePrefixSize;

    public static async Task EncryptAsync(
        Stream plaintext,
        Stream encrypted,
        string password,
        CancellationToken cancellationToken = default)
    {
        ValidateStreams(plaintext, encrypted, password, encrypting: true);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixSize);
        var header = BuildHeader(salt, noncePrefix);
        await encrypted.WriteAsync(header, cancellationToken);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        var buffer = new byte[ChunkSize];
        var ciphertext = new byte[ChunkSize];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            uint counter = 0;
            while (true)
            {
                var length = await FillAsync(plaintext, buffer, cancellationToken);
                if (length == 0)
                {
                    var lengthBytes = new byte[4];
                    var aad = BuildAad(header, counter, lengthBytes);
                    aes.Encrypt(BuildNonce(noncePrefix, counter), ReadOnlySpan<byte>.Empty,
                        Span<byte>.Empty, tag, aad);
                    await encrypted.WriteAsync(lengthBytes, cancellationToken);
                    await encrypted.WriteAsync(tag, cancellationToken);
                    break;
                }

                if (counter == uint.MaxValue) throw new InvalidOperationException("Kopia jest zbyt duża dla tego formatu.");
                var recordLength = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(recordLength, length);
                var recordAad = BuildAad(header, counter, recordLength);
                aes.Encrypt(BuildNonce(noncePrefix, counter), buffer.AsSpan(0, length),
                    ciphertext.AsSpan(0, length), tag, recordAad);
                await encrypted.WriteAsync(recordLength, cancellationToken);
                await encrypted.WriteAsync(ciphertext.AsMemory(0, length), cancellationToken);
                await encrypted.WriteAsync(tag, cancellationToken);
                counter++;
            }
            await encrypted.FlushAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public static async Task DecryptAsync(
        Stream encrypted,
        Stream plaintext,
        string password,
        CancellationToken cancellationToken = default)
    {
        ValidateStreams(encrypted, plaintext, password, encrypting: false);
        var header = new byte[HeaderSize];
        await ReadExactlyAsync(encrypted, header, cancellationToken);
        ValidateHeader(header);
        var iterations = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12, 4));
        var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16, 4));
        var salt = header.AsSpan(20, SaltSize).ToArray();
        var noncePrefix = header.AsSpan(20 + SaltSize, NoncePrefixSize).ToArray();
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        var ciphertext = new byte[chunkSize];
        var cleartext = new byte[chunkSize];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            uint counter = 0;
            while (true)
            {
                var lengthBytes = new byte[4];
                await ReadExactlyAsync(encrypted, lengthBytes, cancellationToken);
                var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                if (length is < 0 || length > chunkSize)
                    throw new InvalidDataException("Plik .fmbak ma nieprawidłową długość fragmentu.");
                if (length > 0) await ReadExactlyAsync(encrypted, ciphertext.AsMemory(0, length), cancellationToken);
                await ReadExactlyAsync(encrypted, tag, cancellationToken);
                try
                {
                    var aad = BuildAad(header, counter, lengthBytes);
                    aes.Decrypt(BuildNonce(noncePrefix, counter), ciphertext.AsSpan(0, length),
                        tag, cleartext.AsSpan(0, length), aad);
                }
                catch (AuthenticationTagMismatchException exception)
                {
                    throw new InvalidDataException(
                        "Nieprawidłowe hasło albo uszkodzona kopia .fmbak.", exception);
                }

                if (length == 0)
                {
                    if (encrypted.ReadByte() != -1)
                        throw new InvalidDataException("Plik .fmbak zawiera dane po bezpiecznym końcu.");
                    break;
                }
                await plaintext.WriteAsync(cleartext.AsMemory(0, length), cancellationToken);
                if (counter == uint.MaxValue) throw new InvalidDataException("Plik .fmbak ma zbyt wiele fragmentów.");
                counter++;
            }
            await plaintext.FlushAsync(cancellationToken);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Plik .fmbak jest niekompletny.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(cleartext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    private static byte[] BuildHeader(byte[] salt, byte[] noncePrefix)
    {
        var header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), FormatVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), Iterations);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), ChunkSize);
        salt.CopyTo(header, 20);
        noncePrefix.CopyTo(header, 20 + SaltSize);
        return header;
    }

    private static void ValidateHeader(byte[] header)
    {
        if (!header.AsSpan(0, 8).SequenceEqual(Magic))
            throw new InvalidDataException("To nie jest plik kopii Firemki.");
        var version = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));
        var iterations = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12, 4));
        var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16, 4));
        if (version != FormatVersion)
            throw new InvalidDataException($"Nieobsługiwana wersja .fmbak: {version}.");
        if (iterations is < 600_000 or > 5_000_000 || chunkSize is < 64 * 1024 or > 4 * 1024 * 1024)
            throw new InvalidDataException("Parametry bezpieczeństwa .fmbak są nieprawidłowe.");
    }

    private static byte[] BuildNonce(byte[] prefix, uint counter)
    {
        var nonce = new byte[12];
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), counter);
        return nonce;
    }

    private static byte[] BuildAad(byte[] header, uint counter, byte[] length)
    {
        var aad = new byte[header.Length + 8];
        header.CopyTo(aad, 0);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(header.Length, 4), counter);
        length.CopyTo(aad, header.Length + 4);
        return aad;
    }

    private static async Task<int> FillAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            total += read;
        }
    }

    private static void ValidateStreams(Stream input, Stream output, string password, bool encrypting)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length < 16) throw new ArgumentException("Hasło odzyskiwania musi mieć co najmniej 16 znaków.", nameof(password));
        if (!input.CanRead) throw new ArgumentException("Strumień wejściowy nie jest czytelny.", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Strumień wyjściowy nie jest zapisywalny.", nameof(output));
        _ = encrypting;
    }
}
