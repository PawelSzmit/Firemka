using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.AnnualClosing;

internal static class AnnualClosingWriteLock
{
    internal static bool UsesPostgres(AppDbContext dbContext)
        => string.Equals(
            dbContext.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);

    internal static async Task AcquireAsync(
        AppDbContext dbContext,
        Guid companyId,
        int taxYear,
        CancellationToken cancellationToken)
    {
        if (!UsesPostgres(dbContext))
        {
            return;
        }

        var lockKey = BinaryPrimitives.ReadInt64LittleEndian(
            SHA256.HashData(Encoding.UTF8.GetBytes($"annual|{companyId:N}|{taxYear}")));
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }
}
