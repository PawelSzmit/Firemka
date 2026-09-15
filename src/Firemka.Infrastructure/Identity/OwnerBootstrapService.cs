using System.Data;
using Firemka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Identity;

public sealed class OwnerBootstrapService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager)
{
    public async Task<OwnerBootstrapStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken);

        if (state.IsCompleted)
        {
            return OwnerBootstrapStatus.Complete;
        }

        return state.OwnerUserId is null
            ? OwnerBootstrapStatus.ReadyToStart
            : OwnerBootstrapStatus.AwaitingTwoFactorSetup;
    }

    public async Task<OwnerCreationResult> TryCreateOwnerAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var state = await GetStateAsync(cancellationToken);
        if (state.OwnerUserId is not null)
        {
            return new OwnerCreationResult(OwnerCreationStatus.AlreadyStarted, null, []);
        }

        var normalizedEmail = email.Trim();
        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new OwnerCreationResult(
                OwnerCreationStatus.InvalidInput,
                null,
                result.Errors.Select(error => error.Description).ToArray());
        }

        state.OwnerUserId = user.Id;
        state.SetupStartedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new OwnerCreationResult(OwnerCreationStatus.Created, user.Id, []);
    }

    public async Task<ApplicationUser?> GetUnfinishedOwnerAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken);
        if (state.OwnerUserId != userId || state.IsCompleted)
        {
            return null;
        }

        return await userManager.FindByIdAsync(userId);
    }

    public async Task<ApplicationUser?> ResumeAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken);
        if (state.OwnerUserId is null || state.IsCompleted)
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user?.Id != state.OwnerUserId || !await userManager.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return user;
    }

    public async Task MarkSetupCompleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken);
        if (state.OwnerUserId != userId)
        {
            throw new InvalidOperationException("Próba zakończenia konfiguracji dla niewłaściwego użytkownika.");
        }

        state.SetupCompletedAtUtc ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<OwnerBootstrap> GetStateAsync(CancellationToken cancellationToken)
    {
        var state = await dbContext.OwnerBootstraps.SingleOrDefaultAsync(
            item => item.Id == OwnerBootstrap.SingletonId,
            cancellationToken);

        if (state is not null)
        {
            return state;
        }

        state = new OwnerBootstrap { Id = OwnerBootstrap.SingletonId };
        dbContext.OwnerBootstraps.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken);
        return state;
    }
}
