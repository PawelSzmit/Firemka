namespace Firemka.Infrastructure.Identity;

public enum OwnerBootstrapStatus
{
    ReadyToStart,
    AwaitingTwoFactorSetup,
    Complete,
}
