using Firemka.Application.Security;
using Firemka.Infrastructure.Persistence;

namespace Firemka.Infrastructure.Security;

public sealed class AuthenticationAuditService(AppDbContext dbContext) : IAuthenticationAuditService
{
    public async Task RecordAsync(AuthenticationAuditRecord record, CancellationToken cancellationToken = default)
    {
        dbContext.LoginAuditEvents.Add(new LoginAuditEvent
        {
            Id = Guid.NewGuid(),
            UserId = record.UserId,
            Outcome = record.Outcome.ToString(),
            Method = record.Method,
            IpAddress = record.IpAddress,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
