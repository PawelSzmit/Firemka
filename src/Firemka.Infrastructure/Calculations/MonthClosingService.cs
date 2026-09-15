using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.MonthClosing;
using Firemka.Domain.Accounting;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Sales;
using Firemka.Domain.TaxYears;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.AnnualClosing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Firemka.Infrastructure.Calculations;

public sealed class MonthClosingService(
    AppDbContext dbContext,
    IAuditTrail auditTrail) : IMonthClosingService
{
    private static readonly TimeZoneInfo WarsawTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public async Task<MonthClosingView?> GetAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(ownerUserId, companyId);
        var assembly = await BuildAsync(
            ownerUserId.Trim(),
            companyId,
            FirstDay(month),
            nowUtc,
            createReferenceRule: true,
            cancellationToken);
        return assembly?.View;
    }

    public async Task<CalculationRuleSetSnapshot> ConfirmRuleSetAsync(
        string ownerUserId,
        Guid ruleSetId,
        ConfirmCalculationRuleSetCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(command);
        if (ruleSetId == Guid.Empty)
        {
            throw new ArgumentException("Zestaw zasad jest wymagany.", nameof(ruleSetId));
        }

        if (!command.IndependentVerificationConfirmed)
        {
            throw new InvalidOperationException(
                "Potwierdź, że zasady zostały sprawdzone przez niezależną księgową lub doradcę.");
        }

        var normalizedOwner = ownerUserId.Trim();
        var source = await dbContext.CalculationRuleSets.SingleOrDefaultAsync(
            item => item.Id == ruleSetId && item.OwnerUserId == normalizedOwner,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono zestawu zasad.");

        var existing = await dbContext.CalculationRuleSets.AsNoTracking().SingleOrDefaultAsync(
            item => item.PreviousRuleSetId == source.Id && item.OwnerUserId == normalizedOwner,
            cancellationToken);
        if (existing is not null)
        {
            if (MatchesConfirmation(existing, command))
            {
                return ToSnapshot(existing);
            }

            throw new InvalidOperationException(
                "Zasady mają już inną wersję potwierdzoną. Odśwież stronę przed dalszą zmianą.");
        }

        var confirmed = source.Confirm(command.EvidenceReference, command.ConfirmedOn, nowUtc);
        dbContext.CalculationRuleSets.Add(confirmed);
        auditTrail.Stage(new AuditRecord(
            "month-calculation-rules-confirmed",
            nameof(CalculationRuleSet),
            confirmed.Id.ToString(),
            normalizedOwner,
            nowUtc,
            JsonSerializer.Serialize(new
            {
                confirmed.CompanyId,
                confirmed.TaxYear,
                confirmed.VersionNumber,
                confirmed.PreviousRuleSetId,
                confirmed.ConfirmedOn,
            })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(confirmed);
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.CalculationRuleSets.AsNoTracking().SingleOrDefaultAsync(
                item => item.PreviousRuleSetId == source.Id && item.OwnerUserId == normalizedOwner,
                cancellationToken);
            if (replay is not null && MatchesConfirmation(replay, command))
            {
                return ToSnapshot(replay);
            }

            throw new InvalidOperationException(
                "Inna zmiana zasad została zapisana równocześnie. Odśwież stronę.",
                exception);
        }
    }

    public async Task<MonthDeclarationSnapshot> SaveDeclarationAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        SaveMonthDeclarationCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(ownerUserId, companyId);
        ArgumentNullException.ThrowIfNull(command);
        var normalizedOwner = ownerUserId.Trim();
        var normalizedMonth = FirstDay(month);
        await RequireOwnedCompanyAsync(normalizedOwner, companyId, cancellationToken);
        await EnsureMonthIsWritableAsync(normalizedOwner, companyId, normalizedMonth, cancellationToken);

        var latest = await dbContext.MonthDeclarations
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == normalizedOwner
                && item.Month == normalizedMonth)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var candidate = latest is null
            ? MonthDeclaration.Create(
                companyId,
                normalizedOwner,
                normalizedMonth,
                command.SocialContributionsDeductible,
                command.PitBaseAdjustment,
                command.HealthIncomeAdjustment,
                command.OpeningVatCarryForward,
                command.OpeningPitAdvancesDue,
                command.OpeningBalancesConfirmed,
                command.HealthIncomeConfirmed,
                command.EvidenceReference,
                nowUtc)
            : latest.CreateRevision(
                command.SocialContributionsDeductible,
                command.PitBaseAdjustment,
                command.HealthIncomeAdjustment,
                command.OpeningVatCarryForward,
                command.OpeningPitAdvancesDue,
                command.OpeningBalancesConfirmed,
                command.HealthIncomeConfirmed,
                command.EvidenceReference,
                nowUtc);

        if (latest?.ValueFingerprint == candidate.ValueFingerprint)
        {
            return ToSnapshot(latest);
        }

        dbContext.MonthDeclarations.Add(candidate);
        auditTrail.Stage(new AuditRecord(
            "month-declaration-saved",
            nameof(MonthDeclaration),
            candidate.Id.ToString(),
            normalizedOwner,
            nowUtc,
            JsonSerializer.Serialize(new
            {
                candidate.CompanyId,
                candidate.Month,
                candidate.VersionNumber,
                candidate.PreviousDeclarationId,
                candidate.OpeningBalancesConfirmed,
                candidate.HealthIncomeConfirmed,
            })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(candidate);
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.MonthDeclarations.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.Month == normalizedMonth)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (winner?.ValueFingerprint == candidate.ValueFingerprint)
            {
                return ToSnapshot(winner);
            }

            throw new InvalidOperationException(
                "Inna deklaracja została zapisana równocześnie. Odśwież stronę.",
                exception);
        }
    }

    public async Task<MonthAdjustmentSnapshot> AddAdjustmentAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        AddMonthAdjustmentCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(ownerUserId, companyId);
        ArgumentNullException.ThrowIfNull(command);
        var normalizedOwner = ownerUserId.Trim();
        var normalizedMonth = FirstDay(month);
        await RequireOwnedCompanyAsync(normalizedOwner, companyId, cancellationToken);
        var candidate = MonthTaxAdjustment.Create(
            companyId,
            normalizedOwner,
            normalizedMonth,
            command.Kind,
            command.Amount,
            command.Reason,
            command.EvidenceReference,
            command.SourceDocumentId,
            command.SalesInvoiceId,
            nowUtc);

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            await EnsureMonthIsWritableAsync(normalizedOwner, companyId, normalizedMonth, cancellationToken);
            await ValidateAdjustmentLinksAsync(
                normalizedOwner,
                companyId,
                normalizedMonth,
                command,
                cancellationToken);

            var existing = await dbContext.MonthTaxAdjustments.AsNoTracking().SingleOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.Month == normalizedMonth
                    && item.ValueFingerprint == candidate.ValueFingerprint,
                cancellationToken);
            if (existing is not null)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return ToSnapshot(existing);
            }

            await ValidateAdjustmentTotalAsync(
                normalizedOwner,
                companyId,
                normalizedMonth,
                candidate,
                cancellationToken);
            dbContext.MonthTaxAdjustments.Add(candidate);
            auditTrail.Stage(new AuditRecord(
                "month-tax-adjustment-added",
                nameof(MonthTaxAdjustment),
                candidate.Id.ToString(),
                normalizedOwner,
                nowUtc,
                JsonSerializer.Serialize(new
                {
                    candidate.CompanyId,
                    candidate.Month,
                    candidate.Kind,
                    candidate.SourceDocumentId,
                    candidate.SalesInvoiceId,
                })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ToSnapshot(candidate);
        }
        catch (Exception exception) when (IsConcurrentWriteException(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.MonthTaxAdjustments.AsNoTracking().SingleOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.Month == normalizedMonth
                    && item.ValueFingerprint == candidate.ValueFingerprint,
                cancellationToken);
            if (replay is not null)
            {
                return ToSnapshot(replay);
            }

            throw new InvalidOperationException(
                "Inna korekta została zapisana równocześnie. Odśwież stronę.",
                exception);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<MonthSettlementSnapshot> CloseAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(ownerUserId, companyId);
        var normalizedOwner = ownerUserId.Trim();
        var normalizedMonth = FirstDay(month);

        _ = await GetAsync(
            normalizedOwner,
            companyId,
            normalizedMonth,
            nowUtc,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono firmy.");

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        string? attemptedInputFingerprint = null;
        try
        {
            await AnnualClosingWriteLock.AcquireAsync(
                dbContext, companyId, normalizedMonth.Year, cancellationToken);
            var assembly = await BuildAsync(
                normalizedOwner,
                companyId,
                normalizedMonth,
                nowUtc,
                createReferenceRule: false,
                cancellationToken)
                ?? throw new KeyNotFoundException("Nie znaleziono firmy.");
            var latest = assembly.LatestSettlement;
            if (latest is not null
                && latest.Status == MonthSettlementStatus.Closed
                && latest.InputFingerprint == assembly.InputFingerprint)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return ToSnapshot(latest);
            }

            if (assembly.Blockers.Count > 0)
            {
                throw new MonthClosingBlockedException(assembly.Blockers);
            }

            if (assembly.Input is null || assembly.RuleSet is null || assembly.Declaration is null)
            {
                throw new InvalidOperationException("Brakuje kompletnego wejścia do kalkulacji.");
            }

            attemptedInputFingerprint = assembly.InputFingerprint;

            var previousVersion = await dbContext.MonthCalculations.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.Month == normalizedMonth)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            var calculation = MonthCalculation.Calculate(
                assembly.Input with { Trust = CalculationTrust.ClosingEligible },
                assembly.RuleSet,
                previousVersion?.VersionNumber + 1 ?? 1,
                previousVersion?.Id,
                nowUtc);
            dbContext.MonthCalculations.Add(calculation);

            MonthSettlement settlement;
            if (latest is null)
            {
                settlement = MonthSettlement.CloseOriginal(
                    companyId,
                    normalizedOwner,
                    normalizedMonth,
                    calculation.Id,
                    calculation.InputFingerprint,
                    nowUtc);
                dbContext.MonthSettlements.Add(settlement);
            }
            else if (latest.Status == MonthSettlementStatus.OpenCorrection)
            {
                settlement = await dbContext.MonthSettlements.SingleAsync(
                    item => item.Id == latest.Id && item.OwnerUserId == normalizedOwner,
                    cancellationToken);
                settlement.Close(calculation.Id, calculation.InputFingerprint, nowUtc);
            }
            else
            {
                throw new MonthClosingBlockedException(
                [
                    Blocker(
                        MonthClosingBlockerCodes.CorrectionRequired,
                        "Najpierw rozpocznij korektę zamkniętego miesiąca.",
                        MonthUrl(normalizedMonth, "history")),
                ]);
            }

            auditTrail.Stage(new AuditRecord(
                "month-closed",
                nameof(MonthSettlement),
                settlement.Id.ToString(),
                normalizedOwner,
                nowUtc,
                JsonSerializer.Serialize(new
                {
                    settlement.CompanyId,
                    settlement.Month,
                    settlement.VersionNumber,
                    calculation.Id,
                    calculation.InputFingerprint,
                })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ToSnapshot(settlement);
        }
        catch (MonthClosingBlockedException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        catch (Exception exception) when (IsConcurrentWriteException(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.MonthSettlements.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.Month == normalizedMonth)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (winner is not null
                && winner.Status == MonthSettlementStatus.Closed
                && string.Equals(
                    winner.InputFingerprint,
                    attemptedInputFingerprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                var calculation = await dbContext.MonthCalculations.AsNoTracking().SingleOrDefaultAsync(
                    item => item.Id == winner.CalculationId
                        && item.InputFingerprint == winner.InputFingerprint,
                    cancellationToken);
                if (calculation is not null)
                {
                    return ToSnapshot(winner);
                }
            }

            throw new InvalidOperationException(
                "Miesiąc został równocześnie zmieniony. Odśwież stronę przed ponowieniem.",
                exception);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<MonthSettlementSnapshot> StartCorrectionAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(ownerUserId, companyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var normalizedOwner = ownerUserId.Trim();
        var normalizedMonth = FirstDay(month);
        await RequireOwnedCompanyAsync(normalizedOwner, companyId, cancellationToken);

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                AnnualClosingWriteLock.UsesPostgres(dbContext)
                    ? IsolationLevel.ReadCommitted
                    : IsolationLevel.Serializable,
                cancellationToken)
            : null;
        Guid? previousSettlementId = null;
        try
        {
            await AnnualClosingWriteLock.AcquireAsync(
                dbContext, companyId, normalizedMonth.Year, cancellationToken);
            var annualClosing = await dbContext.AnnualClosings.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.TaxYear == normalizedMonth.Year)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (annualClosing?.Status == Firemka.Domain.AnnualClosing.AnnualClosingStatus.Closed)
            {
                throw new InvalidOperationException(
                    "Rok jest zamknięty. Najpierw rozpocznij korektę roku.");
            }

            var latest = await dbContext.MonthSettlements
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == normalizedOwner
                    && item.Month == normalizedMonth)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Miesiąc nie ma jeszcze zamkniętej wersji.");
            previousSettlementId = latest.Id;
            if (latest.Status == MonthSettlementStatus.OpenCorrection)
            {
                if (string.Equals(latest.CorrectionReason, reason.Trim(), StringComparison.Ordinal))
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return ToSnapshot(latest);
                }
                throw new InvalidOperationException("Korekta jest już otwarta z innym powodem.");
            }

            var correction = MonthSettlement.StartCorrection(latest, reason, nowUtc);
            dbContext.MonthSettlements.Add(correction);
            auditTrail.Stage(new AuditRecord(
                "month-correction-started",
                nameof(MonthSettlement),
                correction.Id.ToString(),
                normalizedOwner,
                nowUtc,
                JsonSerializer.Serialize(new
                {
                    correction.CompanyId,
                    correction.Month,
                    correction.VersionNumber,
                    correction.PreviousSettlementId,
                })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(correction);
        }
        catch (Exception exception) when (IsConcurrentWriteException(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.MonthSettlements.AsNoTracking().SingleOrDefaultAsync(
                item => item.PreviousSettlementId == previousSettlementId
                    && item.OwnerUserId == normalizedOwner,
                cancellationToken);
            if (replay is not null
                && replay.Status == MonthSettlementStatus.OpenCorrection
                && replay.CorrectionReason == reason.Trim())
            {
                return ToSnapshot(replay);
            }

            throw new InvalidOperationException(
                "Inna korekta została rozpoczęta równocześnie. Odśwież stronę.",
                exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<MonthAssembly?> BuildAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        bool createReferenceRule,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.AsNoTracking()
            .Include(item => item.TaxYears)
            .Include(item => item.VatProfiles)
            .Include(item => item.ZusProfiles)
            .SingleOrDefaultAsync(
                item => item.Id == companyId && item.OwnerUserId == ownerUserId,
                cancellationToken);
        if (company is null)
        {
            return null;
        }

        var ruleSet = await GetLatestRuleSetAsync(company, month.Year, cancellationToken);
        if (ruleSet is null && createReferenceRule && month.Year == 2026)
        {
            ruleSet = await EnsureReferenceRuleAsync(company, nowUtc, cancellationToken);
        }

        var declaration = await dbContext.MonthDeclarations.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == month)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var adjustments = await dbContext.MonthTaxAdjustments.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == month)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var settlements = await dbContext.MonthSettlements.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == month)
            .OrderByDescending(item => item.VersionNumber)
            .ToListAsync(cancellationToken);
        var calculationHistory = await dbContext.MonthCalculations.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == month)
            .OrderByDescending(item => item.VersionNumber)
            .ToListAsync(cancellationToken);
        var latestSettlement = settlements.FirstOrDefault();
        var firstBusinessMonth = FirstDay(company.BusinessStartDate) == month;

        var blockers = new List<MonthClosingBlocker>();
        var taxYear = company.TaxYears.SingleOrDefault(item => item.Year == month.Year);
        if (taxYear is null || taxYear.TaxationForm != TaxationForm.TaxScale)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.TaxYearMissing,
                "Brakuje otwartego roku podatkowego na skali.",
                "/Settings#tax-years");
        }

        if (month >= FirstDay(company.BusinessStartDate))
        {
            if (company.GetVatProfile(month).Profile != VatProfile.ActiveMonthly)
            {
                AddBlocker(
                    blockers,
                    MonthClosingBlockerCodes.VatProfileUnsupported,
                    "Ten kalkulator obsługuje obecnie wyłącznie czynny VAT rozliczany miesięcznie.",
                    "/Settings#vat");
            }

            if (company.GetZusProfile(month).Profile != ZusProfile.HealthOnlyDueToEmployment)
            {
                AddBlocker(
                    blockers,
                    MonthClosingBlockerCodes.ZusProfileUnsupported,
                    "Ten kalkulator obsługuje obecnie wyłącznie profil ZUS z samą składką zdrowotną z uwagi na etat.",
                    "/Settings#zus");
            }
        }

        if (ruleSet is null)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.RuleSetMissing,
                $"Brakuje sprawdzonych zasad obliczeń dla roku {month.Year}.",
                "/Settings/Calculations");
        }
        else if (ruleSet.Trust != CalculationRuleTrust.IndependentlyConfirmed)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.RuleSetUnconfirmed,
                "Zasady są tylko materiałem referencyjnym i wymagają niezależnego potwierdzenia.",
                "/Settings/Calculations");
        }

        if (declaration is null)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.DeclarationMissing,
                "Uzupełnij i potwierdź dane miesięczne.",
                MonthUrl(month, "declaration"));
        }

        if (firstBusinessMonth
            && (declaration is null
                || !declaration.OpeningBalancesConfirmed
                || declaration.OpeningVatCarryForward is null
                || declaration.OpeningPitAdvancesDue is null))
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.OpeningBalancesUnconfirmed,
                "Pierwszy miesiąc wymaga jawnego potwierdzenia sald otwarcia, także gdy wynoszą zero.",
                MonthUrl(month, "declaration"));
        }

        if (declaration is null || !declaration.HealthIncomeConfirmed)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.HealthInputUnconfirmed,
                "Potwierdź osobne dane dochodu do składki zdrowotnej.",
                MonthUrl(month, "declaration"));
        }

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, WarsawTimeZone).DateTime);
        if (localToday < month.AddMonths(1))
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.MonthNotEnded,
                "Miesiąc można zamknąć dopiero po jego zakończeniu.",
                MonthUrl(month));
        }

        MonthCalculation? previousMonthCalculation = null;
        if (!firstBusinessMonth)
        {
            var previousMonth = month.AddMonths(-1);
            var previousSettlement = await dbContext.MonthSettlements.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == ownerUserId
                    && item.Month == previousMonth)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (previousSettlement?.Status != MonthSettlementStatus.Closed
                || previousSettlement.CalculationId is null)
            {
                AddBlocker(
                    blockers,
                    MonthClosingBlockerCodes.PreviousMonthOpen,
                    $"Najpierw zamknij poprzedni miesiąc ({previousMonth:MM.yyyy}).",
                    MonthUrl(previousMonth));
            }
            else
            {
                previousMonthCalculation = await dbContext.MonthCalculations.AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.Id == previousSettlement.CalculationId
                            && item.OwnerUserId == ownerUserId,
                        cancellationToken);
                if (previousMonthCalculation is null)
                {
                    AddBlocker(
                        blockers,
                        MonthClosingBlockerCodes.PreviousMonthOpen,
                        "Poprzednie zamknięcie nie ma kompletnej kalkulacji.",
                        MonthUrl(previousMonth));
                }
            }
        }

        var sales = await dbContext.SalesInvoices.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.ServiceMonth == month)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (sales.Count == 0)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.SalesInvoiceMissing,
                "Brakuje miesięcznej faktury sprzedaży.",
                "/Invoices/Sales");
        }

        if (sales.Any(item => item.Status is not (SalesInvoiceStatus.Issued or SalesInvoiceStatus.IssuedContentMismatch)))
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.SalesInvoiceNotFinal,
                "Faktura sprzedaży nie ma końcowego, przyjętego stanu.",
                "/Invoices/Sales");
        }

        if (sales.Any(item => item.Status == SalesInvoiceStatus.IssuedContentMismatch))
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.SalesContentMismatch,
                "Treść zwrócona przez KSeF różni się od wysłanej faktury.",
                "/Invoices/Sales");
        }

        var unresolvedLateSales = sales.Where(item =>
            item.IssueDate is DateOnly issueDate
            && FirstDay(issueDate) != month
            && !adjustments.Any(adjustment =>
                adjustment.Kind == MonthAdjustmentKind.SalesRecognition
                && adjustment.SalesInvoiceId == item.Id)).ToList();
        if (unresolvedLateSales.Count > 0)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.LateSalesRecognitionUnresolved,
                "Spóźniona faktura wymaga jawnej, udokumentowanej decyzji o okresie rozpoznania.",
                MonthUrl(month, "adjustments"));
        }

        var kpirEntries = await (from entry in dbContext.KpirEntries.AsNoTracking()
                                 join booking in dbContext.CostBookings.AsNoTracking()
                                     on entry.BookingId equals booking.Id
                                 where entry.OwnerUserId == ownerUserId
                                     && booking.CompanyId == companyId
                                     && entry.Period == month
                                     && entry.Included
                                 orderby entry.Id
                                 select entry)
            .ToListAsync(cancellationToken);
        var vatEntries = await (from entry in dbContext.VatPurchaseEntries.AsNoTracking()
                                join booking in dbContext.CostBookings.AsNoTracking()
                                    on entry.BookingId equals booking.Id
                                where entry.OwnerUserId == ownerUserId
                                    && booking.CompanyId == companyId
                                    && entry.Period == month
                                    && entry.Included
                                orderby entry.Id
                                select entry)
            .ToListAsync(cancellationToken);
        var pendingBookings = await dbContext.CostBookings.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Status == CostBookingStatus.PendingReview
                && item.IssueDate.Year == month.Year
                && item.IssueDate.Month == month.Month)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (pendingBookings.Count > 0)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.CostBookingPending,
                "Co najmniej jeden koszt czeka na decyzję księgową.",
                "/Expenses/Review");
        }

        var foreignBookings = await dbContext.CostBookings.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Status != CostBookingStatus.PendingReview
                && item.VatTreatment == VatTreatment.ForeignService
                && (item.KpirPeriod == month || item.VatPeriod == month))
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var unresolvedForeign = foreignBookings.Where(booking =>
            !adjustments.Any(adjustment =>
                adjustment.Kind == MonthAdjustmentKind.ForeignServiceVatOutput
                && adjustment.SourceDocumentId == booking.SourceDocumentId)).ToList();
        if (unresolvedForeign.Count > 0)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.ForeignServiceVatUnresolved,
                "Import usługi wymaga jawnego wpisu VAT należnego powiązanego z dokumentem.",
                MonthUrl(month, "adjustments"));
        }

        var nextMonth = month.AddMonths(1);
        var monthStartUtc = LocalMonthBoundaryUtc(month);
        var nextMonthStartUtc = LocalMonthBoundaryUtc(nextMonth);
        var unresolvedDocuments = await dbContext.SourceDocuments.AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId
                && item.Status != SourceDocumentStatus.UnrelatedToBusiness
                && (item.Status != SourceDocumentStatus.Booked
                    || item.SourceConflicts.Any(conflict => conflict.ResolvedAtUtc == null))
                && ((item.IssueDate != null
                        && item.IssueDate >= month
                        && item.IssueDate < nextMonth)
                    || (item.IssueDate == null
                        && item.CreatedAtUtc >= monthStartUtc
                        && item.CreatedAtUtc < nextMonthStartUtc)))
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (unresolvedDocuments.Count > 0)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.SourceDocumentUnresolved,
                "Co najmniej jeden dokument źródłowy z tego miesiąca nie ma końcowej decyzji.",
                "/Invoices/Incoming");
        }

        var revenueMonth = sales.Sum(item => item.NetAmount)
            + adjustments.Where(item => item.Kind == MonthAdjustmentKind.PitRevenue).Sum(item => item.Amount);
        var costsMonth = kpirEntries.Sum(item => item.Amount)
            + adjustments.Where(item => item.Kind == MonthAdjustmentKind.KpirCost).Sum(item => item.Amount);
        var outputVatMonth = sales.Sum(item => item.VatAmount)
            + adjustments.Where(item => item.Kind is MonthAdjustmentKind.VatOutput
                or MonthAdjustmentKind.ForeignServiceVatOutput).Sum(item => item.Amount);
        var inputVatMonth = vatEntries.Sum(item => item.DeductibleVatAmount)
            + adjustments.Where(item => item.Kind == MonthAdjustmentKind.VatInput).Sum(item => item.Amount);
        if (revenueMonth < 0m || costsMonth < 0m || outputVatMonth < 0m || inputVatMonth < 0m)
        {
            throw new InvalidOperationException(
                "Korekty nie mogą obniżyć sumy przychodu, kosztów ani VAT poniżej zera.");
        }

        var previousIsSameTaxYear = previousMonthCalculation?.Month.Year == month.Year;
        var revenueYtdBefore = previousIsSameTaxYear ? previousMonthCalculation!.RevenueYtd : 0m;
        var costsYtdBefore = previousIsSameTaxYear ? previousMonthCalculation!.CostsYtd : 0m;
        var socialYtdBefore = previousIsSameTaxYear ? previousMonthCalculation!.SocialContributionsYtd : 0m;
        var pitAdjustmentsYtdBefore = previousIsSameTaxYear ? previousMonthCalculation!.PitAdjustmentsYtd : 0m;
        var priorPitAdvances = firstBusinessMonth
            ? declaration?.OpeningPitAdvancesDue ?? 0m
            : previousIsSameTaxYear
                ? previousMonthCalculation!.PriorPitAdvancesDue + previousMonthCalculation.PitAdvanceDue
                : 0m;
        var priorVatCarry = firstBusinessMonth
            ? declaration?.OpeningVatCarryForward ?? 0m
            : previousMonthCalculation?.VatCarryForward ?? 0m;
        var previousHealthIncome = firstBusinessMonth
            ? 0m
            : previousMonthCalculation?.CurrentHealthIncome ?? 0m;

        var sourceLines = BuildSourceLines(
            month,
            declaration,
            sales,
            kpirEntries,
            vatEntries,
            adjustments,
            previousMonthCalculation);
        var inputFingerprint = CreateInputFingerprint(
            companyId,
            month,
            ruleSet,
            declaration,
            previousMonthCalculation,
            sales,
            kpirEntries,
            vatEntries,
            adjustments,
            pendingBookings,
            unresolvedDocuments);

        var hasInputDrift = latestSettlement?.Status == MonthSettlementStatus.Closed
            && !string.Equals(
                latestSettlement.InputFingerprint,
                inputFingerprint,
                StringComparison.OrdinalIgnoreCase);
        if (hasInputDrift)
        {
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.ClosedInputDrift,
                "Dane źródłowe zmieniły się od ostatniego zamknięcia.",
                MonthUrl(month, "sources"));
            AddBlocker(
                blockers,
                MonthClosingBlockerCodes.CorrectionRequired,
                "Aby przeliczyć zmienione dane, rozpocznij korektę.",
                MonthUrl(month, "history"));
        }

        MonthCalculationInput? input = null;
        MonthCalculation? preview = null;
        MonthCalculationSnapshot? calculationSnapshot = null;
        if (ruleSet is not null)
        {
            var declarationId = declaration?.Id ?? MissingDeclarationId(companyId, month);
            var trust = blockers.Count == 0
                ? CalculationTrust.ClosingEligible
                : CalculationTrust.TechnicalPreview;
            input = new MonthCalculationInput(
                companyId,
                ownerUserId,
                month,
                ruleSet.Id,
                declarationId,
                previousMonthCalculation?.Id,
                inputFingerprint,
                revenueMonth,
                costsMonth,
                declaration?.SocialContributionsDeductible ?? 0m,
                declaration?.PitBaseAdjustment ?? 0m,
                (declaration?.HealthIncomeAdjustment ?? 0m)
                    + adjustments.Where(item => item.Kind == MonthAdjustmentKind.HealthIncome).Sum(item => item.Amount),
                revenueYtdBefore,
                costsYtdBefore,
                socialYtdBefore,
                pitAdjustmentsYtdBefore,
                priorPitAdvances,
                priorVatCarry,
                previousHealthIncome,
                outputVatMonth,
                inputVatMonth,
                trust,
                sourceLines);

            if (latestSettlement?.Status == MonthSettlementStatus.Closed
                && !hasInputDrift
                && latestSettlement.CalculationId is Guid storedCalculationId)
            {
                var stored = calculationHistory.SingleOrDefault(item => item.Id == storedCalculationId);
                calculationSnapshot = stored is null ? null : ToSnapshot(stored);
            }

            if (calculationSnapshot is null)
            {
                preview = MonthCalculation.Calculate(input, ruleSet, 1, null, nowUtc);
                calculationSnapshot = ToSnapshot(preview);
            }
        }

        var adjustmentSnapshots = adjustments.Select(ToSnapshot).ToArray();
        var settlementSnapshots = settlements.Select(ToSnapshot).ToArray();
        var view = new MonthClosingView(
            company.Id,
            company.Name,
            month,
            firstBusinessMonth,
            ruleSet is null ? null : ToSnapshot(ruleSet),
            declaration is null ? null : ToSnapshot(declaration),
            adjustmentSnapshots,
            calculationSnapshot,
            calculationHistory.Select(ToSnapshot).ToArray(),
            latestSettlement is null ? null : ToSnapshot(latestSettlement),
            settlementSnapshots,
            blockers,
            hasInputDrift);
        return new MonthAssembly(
            view,
            ruleSet,
            declaration,
            input,
            inputFingerprint,
            latestSettlement,
            blockers);
    }

    private async Task<CalculationRuleSet?> EnsureReferenceRuleAsync(
        Company company,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var existing = await GetLatestRuleSetAsync(company, 2026, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var reference = CalculationReferenceCatalog.Create2026(company.Id, company.OwnerUserId, nowUtc);
        dbContext.CalculationRuleSets.Add(reference);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return reference;
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await GetLatestRuleSetAsync(company, 2026, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }

            throw new InvalidOperationException(
                "Nie udało się utworzyć referencyjnego zestawu zasad.",
                exception);
        }
    }

    private Task<CalculationRuleSet?> GetLatestRuleSetAsync(
        Company company,
        int taxYear,
        CancellationToken cancellationToken)
        => dbContext.CalculationRuleSets.AsNoTracking()
            .Where(item => item.CompanyId == company.Id
                && item.OwnerUserId == company.OwnerUserId
                && item.TaxYear == taxYear)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task ValidateAdjustmentLinksAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        AddMonthAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourceDocumentId is Guid sourceDocumentId)
        {
            var document = await dbContext.SourceDocuments.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == sourceDocumentId && item.OwnerUserId == ownerUserId,
                cancellationToken)
                ?? throw new KeyNotFoundException("Nie znaleziono dokumentu źródłowego.");
            if (ResolveDocumentMonth(document) != month)
            {
                throw new InvalidOperationException("Dokument źródłowy dotyczy innego miesiąca.");
            }
        }

        if (command.SalesInvoiceId is Guid salesInvoiceId)
        {
            var ownsInvoice = await dbContext.SalesInvoices.AsNoTracking().AnyAsync(
                item => item.Id == salesInvoiceId
                    && item.CompanyId == companyId
                    && item.OwnerUserId == ownerUserId
                    && item.ServiceMonth == month,
                cancellationToken);
            if (!ownsInvoice)
            {
                throw new KeyNotFoundException("Nie znaleziono faktury sprzedaży z tego miesiąca.");
            }
        }
    }

    private async Task ValidateAdjustmentTotalAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        MonthTaxAdjustment candidate,
        CancellationToken cancellationToken)
    {
        if (candidate.Kind == MonthAdjustmentKind.HealthIncome)
        {
            return;
        }

        var existing = await dbContext.MonthTaxAdjustments.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == month
                && item.Kind == candidate.Kind)
            .SumAsync(item => item.Amount, cancellationToken);
        decimal sourceTotal = candidate.Kind switch
        {
            MonthAdjustmentKind.PitRevenue => await dbContext.SalesInvoices.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == ownerUserId
                    && item.ServiceMonth == month)
                .SumAsync(item => item.NetAmount, cancellationToken),
            MonthAdjustmentKind.KpirCost => await (from entry in dbContext.KpirEntries.AsNoTracking()
                                                   join booking in dbContext.CostBookings.AsNoTracking()
                                                       on entry.BookingId equals booking.Id
                                                   where entry.OwnerUserId == ownerUserId
                                                       && booking.CompanyId == companyId
                                                       && entry.Period == month
                                                       && entry.Included
                                                   select entry.Amount)
                .SumAsync(cancellationToken),
            MonthAdjustmentKind.VatInput => await (from entry in dbContext.VatPurchaseEntries.AsNoTracking()
                                                   join booking in dbContext.CostBookings.AsNoTracking()
                                                       on entry.BookingId equals booking.Id
                                                   where entry.OwnerUserId == ownerUserId
                                                       && booking.CompanyId == companyId
                                                       && entry.Period == month
                                                       && entry.Included
                                                   select entry.DeductibleVatAmount)
                .SumAsync(cancellationToken),
            MonthAdjustmentKind.VatOutput or MonthAdjustmentKind.ForeignServiceVatOutput =>
                await dbContext.SalesInvoices.AsNoTracking()
                    .Where(item => item.CompanyId == companyId
                        && item.OwnerUserId == ownerUserId
                        && item.ServiceMonth == month)
                    .SumAsync(item => item.VatAmount, cancellationToken)
                + await dbContext.MonthTaxAdjustments.AsNoTracking()
                    .Where(item => item.CompanyId == companyId
                        && item.OwnerUserId == ownerUserId
                        && item.Month == month
                        && (item.Kind == MonthAdjustmentKind.VatOutput
                            || item.Kind == MonthAdjustmentKind.ForeignServiceVatOutput)
                        && item.Kind != candidate.Kind)
                    .SumAsync(item => item.Amount, cancellationToken),
            _ => 0m,
        };

        if (sourceTotal + existing + candidate.Amount < 0m)
        {
            throw new InvalidOperationException(
                "Korekta nie może obniżyć miesięcznej sumy poniżej zera.");
        }
    }

    private async Task EnsureMonthIsWritableAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken)
    {
        var latest = await dbContext.MonthSettlements.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == month)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest?.Status == MonthSettlementStatus.Closed)
        {
            throw new InvalidOperationException(
                "Miesiąc jest zamknięty. Najpierw rozpocznij korektę.");
        }
    }

    private async Task RequireOwnedCompanyAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Companies.AsNoTracking().AnyAsync(
            item => item.Id == companyId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("Nie znaleziono firmy.");
        }
    }

    private static IReadOnlyCollection<CalculationSourceLine> BuildSourceLines(
        DateOnly month,
        MonthDeclaration? declaration,
        IReadOnlyCollection<SalesInvoice> sales,
        IReadOnlyCollection<KpirEntry> kpirEntries,
        IReadOnlyCollection<VatPurchaseEntry> vatEntries,
        IReadOnlyCollection<MonthTaxAdjustment> adjustments,
        MonthCalculation? previousMonthCalculation)
    {
        var lines = new List<CalculationSourceLine>();
        if (declaration is null)
        {
            lines.Add(new CalculationSourceLine(
                "DECLARATION:MISSING",
                "Nieuzupełnione dane miesięczne (wartości robocze zero)",
                0m,
                MonthUrl(month, "declaration")));
        }
        else
        {
            lines.Add(new CalculationSourceLine(
                $"DECLARATION:SOCIAL:{declaration.Id:N}",
                "Składki społeczne spoza KPiR",
                declaration.SocialContributionsDeductible,
                MonthUrl(month, "declaration")));
            lines.Add(new CalculationSourceLine(
                $"DECLARATION:PIT:{declaration.Id:N}",
                "Korekta podstawy PIT z deklaracji",
                declaration.PitBaseAdjustment,
                MonthUrl(month, "declaration")));
            lines.Add(new CalculationSourceLine(
                $"DECLARATION:HEALTH:{declaration.Id:N}",
                "Korekta dochodu zdrowotnego z deklaracji",
                declaration.HealthIncomeAdjustment,
                MonthUrl(month, "declaration")));
        }

        foreach (var invoice in sales.OrderBy(item => item.Id))
        {
            var reference = $"/Invoices/Sales/Details?id={invoice.Id}";
            lines.Add(new CalculationSourceLine(
                $"SALE:NET:{invoice.Id:N}",
                $"Przychód netto {invoice.InvoiceNumber ?? "wersji roboczej"}",
                invoice.NetAmount,
                reference));
            lines.Add(new CalculationSourceLine(
                $"SALE:VAT:{invoice.Id:N}",
                $"VAT należny {invoice.InvoiceNumber ?? "wersji roboczej"}",
                invoice.VatAmount,
                reference));
        }

        foreach (var entry in kpirEntries.OrderBy(item => item.Id))
        {
            lines.Add(new CalculationSourceLine(
                $"KPIR:{entry.Id:N}",
                $"KPiR: {entry.Category}",
                entry.Amount,
                $"/Books?year={month.Year}"));
        }

        foreach (var entry in vatEntries.OrderBy(item => item.Id))
        {
            lines.Add(new CalculationSourceLine(
                $"VAT:INPUT:{entry.Id:N}",
                "VAT naliczony do odliczenia",
                entry.DeductibleVatAmount,
                $"/Books?year={month.Year}"));
        }

        foreach (var adjustment in adjustments.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id))
        {
            lines.Add(new CalculationSourceLine(
                $"ADJUSTMENT:{adjustment.Id:N}",
                $"Korekta: {adjustment.Reason}",
                adjustment.Amount,
                MonthUrl(month, "adjustments")));
        }

        if (previousMonthCalculation is not null)
        {
            lines.Add(new CalculationSourceLine(
                $"PREVIOUS:{previousMonthCalculation.Id:N}",
                "Poprzednia zamknięta kalkulacja",
                previousMonthCalculation.VatCarryForward,
                MonthUrl(month.AddMonths(-1), "history")));
        }

        return lines;
    }

    private static string CreateInputFingerprint(
        Guid companyId,
        DateOnly month,
        CalculationRuleSet? ruleSet,
        MonthDeclaration? declaration,
        MonthCalculation? previousMonthCalculation,
        IReadOnlyCollection<SalesInvoice> sales,
        IReadOnlyCollection<KpirEntry> kpirEntries,
        IReadOnlyCollection<VatPurchaseEntry> vatEntries,
        IReadOnlyCollection<MonthTaxAdjustment> adjustments,
        IReadOnlyCollection<CostBooking> pendingBookings,
        IReadOnlyCollection<SourceDocument> unresolvedDocuments)
    {
        var parts = new List<string>
        {
            $"company:{companyId:N}",
            $"month:{month:yyyy-MM-dd}",
            $"rule:{ruleSet?.Id.ToString("N") ?? "missing"}",
            $"declaration:{declaration?.Id.ToString("N") ?? "missing"}:{declaration?.ValueFingerprint ?? "missing"}",
            $"previous:{previousMonthCalculation?.Id.ToString("N") ?? "missing"}",
        };
        parts.AddRange(sales.OrderBy(item => item.Id).Select(item => FormattableString.Invariant(
            $"sale:{item.Id:N}:{item.Status}:{item.NetAmount:G29}:{item.VatAmount:G29}:{item.IssueDate:yyyy-MM-dd}:{item.ConcurrencyStamp:N}")));
        parts.AddRange(kpirEntries.OrderBy(item => item.Id).Select(item => FormattableString.Invariant(
            $"kpir:{item.Id:N}:{item.Amount:G29}:{item.Included}")));
        parts.AddRange(vatEntries.OrderBy(item => item.Id).Select(item => FormattableString.Invariant(
            $"vat:{item.Id:N}:{item.DeductibleVatAmount:G29}:{item.Included}")));
        parts.AddRange(adjustments.OrderBy(item => item.Id).Select(item =>
            $"adjustment:{item.Id:N}:{item.ValueFingerprint}"));
        parts.AddRange(pendingBookings.OrderBy(item => item.Id).Select(item =>
            $"pending:{item.Id:N}:{item.ConcurrencyStamp:N}"));
        parts.AddRange(unresolvedDocuments.OrderBy(item => item.Id).Select(item =>
            $"document:{item.Id:N}:{item.Status}:{item.StateVersion}:{item.HasSourceConflict}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }

    private static DateOnly ResolveDocumentMonth(SourceDocument document)
    {
        if (document.IssueDate is DateOnly issueDate)
        {
            return FirstDay(issueDate);
        }

        var local = TimeZoneInfo.ConvertTime(document.CreatedAtUtc, WarsawTimeZone);
        return new DateOnly(local.Year, local.Month, 1);
    }

    private static DateTimeOffset LocalMonthBoundaryUtc(DateOnly month)
    {
        var localMidnight = DateTime.SpecifyKind(
            month.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, WarsawTimeZone));
    }

    private static Guid MissingDeclarationId(Guid companyId, DateOnly month)
    {
        var value = Encoding.UTF8.GetBytes($"missing-declaration:{companyId:N}:{month:yyyyMM}");
        return new Guid(SHA256.HashData(value).AsSpan(0, 16));
    }

    private static void AddBlocker(
        ICollection<MonthClosingBlocker> blockers,
        string code,
        string message,
        string actionUrl)
    {
        if (!blockers.Any(item => item.Code == code))
        {
            blockers.Add(Blocker(code, message, actionUrl));
        }
    }

    private static MonthClosingBlocker Blocker(string code, string message, string actionUrl)
        => new(code, message, actionUrl);

    private static string MonthUrl(DateOnly month, string? fragment = null)
        => $"/Settlements/Month?year={month.Year}&month={month.Month}"
            + (string.IsNullOrWhiteSpace(fragment) ? string.Empty : $"#{fragment}");

    private static bool MatchesConfirmation(
        CalculationRuleSet rule,
        ConfirmCalculationRuleSetCommand command)
        => rule.Trust == CalculationRuleTrust.IndependentlyConfirmed
            && rule.IndependentEvidenceReference == command.EvidenceReference.Trim()
            && rule.ConfirmedOn == command.ConfirmedOn;

    private static void ValidateRequest(string ownerUserId, Guid companyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }
    }

    private static DateOnly FirstDay(DateOnly date) => new(date.Year, date.Month, 1);

    private static bool IsConcurrentWriteException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is PostgresException postgresException
                && postgresException.SqlState is PostgresErrorCodes.UniqueViolation
                    or PostgresErrorCodes.SerializationFailure
                    or PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    private static CalculationRuleSetSnapshot ToSnapshot(CalculationRuleSet value)
        => new(
            value.Id,
            value.CompanyId,
            value.TaxYear,
            value.VersionNumber,
            value.PreviousRuleSetId,
            value.Trust,
            value.Values,
            value.OfficialSources,
            value.CapturedOn,
            value.IndependentEvidenceReference,
            value.ConfirmedOn,
            value.RuleFingerprint,
            value.CreatedAtUtc);

    private static MonthDeclarationSnapshot ToSnapshot(MonthDeclaration value)
        => new(
            value.Id,
            value.CompanyId,
            value.Month,
            value.VersionNumber,
            value.PreviousDeclarationId,
            value.SocialContributionsDeductible,
            value.PitBaseAdjustment,
            value.HealthIncomeAdjustment,
            value.OpeningVatCarryForward,
            value.OpeningPitAdvancesDue,
            value.OpeningBalancesConfirmed,
            value.HealthIncomeConfirmed,
            value.EvidenceReference,
            value.ValueFingerprint,
            value.CreatedAtUtc);

    private static MonthAdjustmentSnapshot ToSnapshot(MonthTaxAdjustment value)
        => new(
            value.Id,
            value.CompanyId,
            value.Month,
            value.Kind,
            value.Amount,
            value.Reason,
            value.EvidenceReference,
            value.SourceDocumentId,
            value.SalesInvoiceId,
            value.ValueFingerprint,
            value.CreatedAtUtc);

    private static MonthCalculationSnapshot ToSnapshot(MonthCalculation value)
        => new(
            value.Id,
            value.CompanyId,
            value.Month,
            value.VersionNumber,
            value.RuleSetId,
            value.DeclarationId,
            value.PreviousMonthCalculationId,
            value.PreviousVersionId,
            value.InputFingerprint,
            value.Trust,
            value.RevenueMonth,
            value.CostsMonth,
            value.SocialContributionsMonth,
            value.PitBaseAdjustmentMonth,
            value.HealthIncomeAdjustmentMonth,
            value.RevenueYtd,
            value.CostsYtd,
            value.SocialContributionsYtd,
            value.PitAdjustmentsYtd,
            value.PitIncomeYtd,
            value.PitTaxBase,
            value.CumulativePitTax,
            value.PriorPitAdvancesDue,
            value.PitAdvanceDue,
            value.CanDeferPitPayment,
            value.OutputVatMonth,
            value.InputVatMonth,
            value.PriorVatCarryForward,
            value.VatPayable,
            value.VatPayableRounded,
            value.VatCarryForward,
            value.CurrentHealthIncome,
            value.PreviousMonthHealthIncome,
            value.HealthMinimumBase,
            value.HealthBasis,
            value.HealthContribution,
            value.Lines.OrderBy(item => item.Sequence).Select(item => new MonthCalculationLineSnapshot(
                item.Sequence,
                item.Code,
                item.Label,
                item.Amount,
                item.SourceReference,
                item.IsFormula)).ToArray(),
            value.CreatedAtUtc);

    private static MonthSettlementSnapshot ToSnapshot(MonthSettlement value)
        => new(
            value.Id,
            value.CompanyId,
            value.Month,
            value.VersionNumber,
            value.PreviousSettlementId,
            value.Status,
            value.CorrectionReason,
            value.CalculationId,
            value.InputFingerprint,
            value.CreatedAtUtc,
            value.ClosedAtUtc);

    private sealed record MonthAssembly(
        MonthClosingView View,
        CalculationRuleSet? RuleSet,
        MonthDeclaration? Declaration,
        MonthCalculationInput? Input,
        string InputFingerprint,
        MonthSettlement? LatestSettlement,
        IReadOnlyList<MonthClosingBlocker> Blockers);
}
