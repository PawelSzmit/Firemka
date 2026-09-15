namespace Firemka.Application.Security;

public enum AuthenticationAuditOutcome
{
    Successful,
    Failed,
    LockedOut,
    TrustedDeviceRevoked,
}
