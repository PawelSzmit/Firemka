namespace Firemka.Application.Security;

public interface IAuthenticationAuditService
{
    Task RecordAsync(AuthenticationAuditRecord record, CancellationToken cancellationToken = default);
}
