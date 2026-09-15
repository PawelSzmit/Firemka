using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Firemka.Application.AnnualClosing;
using Firemka.Application.Auditing;
using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Application.Time;
using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Pdf;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AnnualClosingEntity = Firemka.Domain.AnnualClosing.AnnualClosing;

namespace Firemka.Infrastructure.AnnualClosing;

public sealed class AnnualClosingService(
    AppDbContext dbContext,
    PrivateFileStore privateFileStore,
    IJpkPkpir3Generator jpkGenerator,
    IAnnualReportPdfGenerator pdfGenerator,
    IMonthClosingService monthClosingService,
    IAnnualArchiveRequestQueue archiveQueue,
    IAuditTrail auditTrail) : IAnnualClosingService
{
    private const string JpkGeneratorVersion = "firemka-jpk-pkpir3-annual-r1";
    private const string OfficialSources = "MF PIT-36 i PIT/B; MF JPK_PKPIR(3); ZUS roczne rozliczenie składki zdrowotnej";

    public async Task<AnnualClosingView> GetAsync(
        string ownerUserId, Guid companyId, int taxYear, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var assembly = await AssembleAsync(ownerUserId, companyId, taxYear, nowUtc, cancellationToken);
        return assembly.View;
    }

    public async Task<AnnualDeclarationSnapshot> SaveDeclarationAsync(
        string ownerUserId, Guid companyId, int taxYear, SaveAnnualDeclarationCommand command,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var owner = ValidateIdentity(ownerUserId, companyId, taxYear);
        await RequireOwnedCompanyAsync(owner, companyId, taxYear, cancellationToken);
        if (command.ConfirmedOn > PolishBusinessTime.GetDate(nowUtc))
            throw new ArgumentOutOfRangeException(nameof(command), "Data potwierdzenia nie może przypadać w przyszłości.");
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                AnnualClosingWriteLock.UsesPostgres(dbContext)
                    ? IsolationLevel.ReadCommitted
                    : IsolationLevel.Serializable,
                cancellationToken)
            : null;
        try
        {
            await AnnualClosingWriteLock.AcquireAsync(dbContext, companyId, taxYear, cancellationToken);
            await EnsureAnnualCorrectionWritableAsync(owner, companyId, taxYear, cancellationToken);
            var latest = await dbContext.AnnualDeclarations
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            if (latest is not null && Matches(latest, command))
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return ToSnapshot(latest);
            }

            var declaration = AnnualDeclaration.Create(
                companyId, owner, taxYear, (latest?.VersionNumber ?? 0) + 1, latest?.Id,
                command.OpeningInventory, command.ClosingInventory, command.PitAdvancesPaid,
                command.HealthContributionsPaid, command.IndependentVerificationConfirmed,
                command.EvidenceReference, command.ConfirmedOn, nowUtc, PolishBusinessTime.GetDate(nowUtc));
            dbContext.AnnualDeclarations.Add(declaration);
            auditTrail.Stage(new AuditRecord("annual-declaration-confirmed", nameof(AnnualDeclaration),
                declaration.Id.ToString(), owner, nowUtc,
                JsonSerializer.Serialize(new { declaration.CompanyId, declaration.TaxYear, declaration.VersionNumber })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(declaration);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.AnnualDeclarations.AsNoTracking()
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            if (winner is not null && Matches(winner, command)) return ToSnapshot(winner);
            throw new InvalidOperationException(
                "Dane roczne zostały równocześnie zmienione. Odśwież stronę.", exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AnnualClosingSnapshot> CloseAsync(
        string ownerUserId, Guid companyId, int taxYear, ConfirmAnnualClosingCommand command, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.IndependentResultConfirmed)
            throw new ArgumentException("Potwierdź niezależne sprawdzenie rocznego podsumowania.", nameof(command));
        var owner = ValidateIdentity(ownerUserId, companyId, taxYear);
        var descriptors = new List<PrivateFileDescriptor>();
        var usesPostgres = AnnualClosingWriteLock.UsesPostgres(dbContext);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                usesPostgres ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            await AnnualClosingWriteLock.AcquireAsync(dbContext, companyId, taxYear, cancellationToken);

            var assembly = await AssembleAsync(owner, companyId, taxYear, nowUtc, cancellationToken);
            if (assembly.View.Blockers.Count > 0) throw new AnnualClosingBlockedException(assembly.View.Blockers);
            var declaration = assembly.Declaration!;
            var values = assembly.Values!;
            var profile = assembly.Profile!;
            var latest = await dbContext.AnnualClosings
                .Include(item => item.Months)
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            var version = latest?.VersionNumber ?? 1;
            var fingerprint = CreateFingerprint(declaration, profile.Id, assembly.Months, values, version);
            if (latest?.Status == AnnualClosingStatus.Closed)
            {
                if (string.Equals(latest.InputFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return ToSnapshot(latest);
                }
                throw new AnnualClosingBlockedException([
                    new(AnnualClosingBlockerCodes.CorrectionRequired,
                        "Rok jest już zamknięty. Najpierw rozpocznij korektę roku.",
                        $"/Settlements/Year?year={taxYear}")]);
            }

            var rows = await BuildJpkRowsAsync(assembly.Company, owner, taxYear, cancellationToken);
            if (rows.Count == 0)
                throw new InvalidOperationException("Roczny JPK_PKPIR nie może być pusty. Sprawdź księgę roku.");
            if (rows.Sum(item => item.Revenue) != values.Revenue
                || rows.Sum(item => item.OtherExpense) != values.CostsBeforeInventory)
                throw new InvalidOperationException("Roczny JPK_PKPIR nie zgadza się z sumą zamkniętych miesięcy.");
            var identity = new FilingPersonIdentity(
                assembly.Company.Nip, profile.FirstName, profile.LastName, profile.BirthDate,
                profile.Pesel, profile.TaxOfficeCode, profile.ZusInsuranceTitleCode, profile.Email);
            var xml = jpkGenerator.Generate(new JpkPkpir3Input(
                identity, new DateOnly(taxYear, 1, 1), new DateOnly(taxYear, 12, 31), nowUtc,
                version == 1 ? 0 : 1, rows, declaration.OpeningInventory, declaration.ClosingInventory));
            jpkGenerator.Validate(xml);

            var pdf = pdfGenerator.Generate(new AnnualReportPdfInput(
                assembly.Company.Name, assembly.Company.Nip, assembly.Company.Address,
                $"{profile.FirstName} {profile.LastName}", taxYear, version, values, assembly.Months,
                declaration.EvidenceReference, declaration.ConfirmedOn, nowUtc, OfficialSources));

            var jpkDescriptor = await privateFileStore.SaveAsync(new PrivateFileUpload(
                $"JPK_PKPIR-{taxYear}-v{version}.xml", "application/xml",
                new MemoryStream(new UTF8Encoding(false).GetBytes(xml), writable: false)), cancellationToken);
            descriptors.Add(jpkDescriptor);
            var pdfDescriptor = await privateFileStore.SaveAsync(new PrivateFileUpload(
                $"Firemka-zestawienie-roczne-{taxYear}-v{version}.pdf", "application/pdf",
                new MemoryStream(pdf, writable: false)), cancellationToken);
            descriptors.Add(pdfDescriptor);
            var jpkFileId = Guid.NewGuid();
            var pdfFileId = Guid.NewGuid();

            AnnualClosingEntity closing;
            var monthInputs = assembly.Months.Select(ToDomainInput).ToArray();
            if (latest is null)
            {
                closing = AnnualClosingEntity.CloseOriginal(
                    companyId, owner, taxYear, declaration.Id, values, monthInputs,
                    jpkFileId, jpkDescriptor.Sha256, JpkGeneratorVersion,
                    pdfFileId, pdfDescriptor.Sha256, AnnualReportPdfGenerator.Version,
                    fingerprint, command.IndependentResultConfirmed, nowUtc);
                dbContext.AnnualClosings.Add(closing);
            }
            else
            {
                closing = latest;
                closing.Close(declaration.Id, values, monthInputs,
                    jpkFileId, jpkDescriptor.Sha256, JpkGeneratorVersion,
                    pdfFileId, pdfDescriptor.Sha256, AnnualReportPdfGenerator.Version,
                    fingerprint, command.IndependentResultConfirmed, nowUtc);
                dbContext.AnnualClosingMonths.AddRange(closing.Months);
            }

            dbContext.StoredFiles.Add(CreateStoredFile(jpkFileId, owner, closing.Id, version,
                StoredFileRecordType.AnnualJpk, jpkDescriptor, nowUtc));
            dbContext.StoredFiles.Add(CreateStoredFile(pdfFileId, owner, closing.Id, version,
                StoredFileRecordType.AnnualReport, pdfDescriptor, nowUtc));
            archiveQueue.Stage(companyId, owner, taxYear, closing.Id, nowUtc);
            auditTrail.Stage(new AuditRecord("annual-closing-closed", nameof(AnnualClosingEntity),
                closing.Id.ToString(), owner, nowUtc,
                JsonSerializer.Serialize(new { closing.CompanyId, closing.TaxYear, closing.VersionNumber, closing.InputFingerprint })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(closing);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            foreach (var descriptor in descriptors)
                await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<AnnualClosingSnapshot> StartCorrectionAsync(
        string ownerUserId, Guid companyId, int taxYear, string reason, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateIdentity(ownerUserId, companyId, taxYear);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await RequireOwnedCompanyAsync(owner, companyId, taxYear, cancellationToken);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                AnnualClosingWriteLock.UsesPostgres(dbContext)
                    ? IsolationLevel.ReadCommitted
                    : IsolationLevel.Serializable,
                cancellationToken)
            : null;
        try
        {
            await AnnualClosingWriteLock.AcquireAsync(dbContext, companyId, taxYear, cancellationToken);
            var latest = await dbContext.AnnualClosings.Include(item => item.Months)
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Rok nie ma jeszcze zamkniętej wersji.");
            if (latest.Status == AnnualClosingStatus.OpenCorrection)
            {
                if (string.Equals(latest.CorrectionReason, reason.Trim(), StringComparison.Ordinal))
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return ToSnapshot(latest);
                }
                throw new InvalidOperationException("Korekta roku jest już rozpoczęta z innym powodem.");
            }
            var correction = AnnualClosingEntity.StartCorrection(latest, reason, nowUtc);
            dbContext.AnnualClosings.Add(correction);
            auditTrail.Stage(new AuditRecord("annual-closing-correction-started", nameof(AnnualClosingEntity),
                correction.Id.ToString(), owner, nowUtc,
                JsonSerializer.Serialize(new { correction.CompanyId, correction.TaxYear, correction.VersionNumber, correction.PreviousClosingId })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(correction);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.AnnualClosings.AsNoTracking().Include(item => item.Months)
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            if (replay?.Status == AnnualClosingStatus.OpenCorrection
                && string.Equals(replay.CorrectionReason, reason.Trim(), StringComparison.Ordinal))
                return ToSnapshot(replay);
            throw new InvalidOperationException(
                "Inna korekta roku została rozpoczęta równocześnie. Odśwież stronę.", exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<AnnualClosingSnapshot> ApproveJpkAsync(
        string ownerUserId, Guid annualClosingId, string evidence, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
        => ChangeJpkAsync(ownerUserId, annualClosingId,
            closing => closing.ApproveJpk(evidence, nowUtc),
            "annual-jpk-approved", nowUtc, cancellationToken);

    public Task<AnnualClosingSnapshot> MarkJpkSentAsync(
        string ownerUserId, Guid annualClosingId, string manualSubmissionReference,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        => ChangeJpkAsync(ownerUserId, annualClosingId,
            closing => closing.MarkJpkSent(manualSubmissionReference, nowUtc),
            "annual-jpk-marked-sent-manually", nowUtc, cancellationToken);

    public async Task<AnnualClosingSnapshot> RecordJpkOutcomeAsync(
        string ownerUserId, Guid annualClosingId, FilingSubmissionOutcome outcome,
        FilingReceiptUpload receipt, string reference, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var owner = ownerUserId?.Trim() ?? string.Empty;
        var closing = await RequireAnnualClosingAsync(owner, annualClosingId, cancellationToken);
        PrivateFileDescriptor? descriptor = null;
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            descriptor = await privateFileStore.SaveAsync(new PrivateFileUpload(
                receipt.FileName, NormalizeReceiptMediaType(receipt.FileName, receipt.MediaType),
                receipt.Content), cancellationToken);
            var storedFile = new StoredFile
            {
                Id = Guid.NewGuid(),
                OwnerUserId = owner,
                StorageKey = descriptor.StorageKey,
                OriginalFileName = descriptor.OriginalFileName,
                MediaType = descriptor.MediaType,
                SizeBytes = descriptor.SizeBytes,
                Sha256 = descriptor.Sha256,
                Origin = StoredFileOrigin.ManualUpload,
                RecordType = StoredFileRecordType.SubmissionReceipt,
                RecordId = closing.Id,
                RecordVersion = closing.VersionNumber,
                CreatedAtUtc = nowUtc,
            };
            closing.RecordJpkOutcome(outcome, storedFile.Id, reference, nowUtc);
            dbContext.StoredFiles.Add(storedFile);
            StageJpkAudit("annual-jpk-outcome-recorded", closing, owner, nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(closing);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            if (descriptor is not null)
                await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var winner = await RequireAnnualClosingAsync(owner, annualClosingId, cancellationToken);
            var winnerReceiptSha = winner.JpkReceiptStoredFileId is Guid winnerReceiptId
                ? await dbContext.StoredFiles.AsNoTracking()
                    .Where(item => item.Id == winnerReceiptId && item.OwnerUserId == owner)
                    .Select(item => item.Sha256)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            var expectedStatus = outcome == FilingSubmissionOutcome.Accepted
                ? FilingArtifactStatus.Accepted
                : FilingArtifactStatus.Rejected;
            if (descriptor is not null
                && winner.JpkStatus == expectedStatus
                && string.Equals(winner.JpkOutcomeReference, reference.Trim(), StringComparison.Ordinal)
                && string.Equals(winnerReceiptSha, descriptor.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return ToSnapshot(winner);
            }
            throw new InvalidOperationException(
                "Wynik tej wersji rocznego JPK został równocześnie zapisany inaczej. Odśwież stronę.",
                exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            if (descriptor is not null)
                await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<Assembly> AssembleAsync(
        string ownerUserId, Guid companyId, int taxYear, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var owner = ValidateIdentity(ownerUserId, companyId, taxYear);
        var company = await dbContext.Companies.Include(item => item.Counterparty).Include(item => item.TaxYears)
            .SingleOrDefaultAsync(item => item.Id == companyId && item.OwnerUserId == owner, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono firmy.");
        if (!company.TaxYears.Any(item => item.Year == taxYear))
            throw new KeyNotFoundException($"Rok {taxYear} nie jest otwarty w profilu firmy.");

        var declaration = await dbContext.AnnualDeclarations.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        var profile = await dbContext.FilingProfileVersions.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        var closings = await dbContext.AnnualClosings.AsNoTracking().Include(item => item.Months)
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == taxYear)
            .OrderByDescending(item => item.VersionNumber).ToListAsync(cancellationToken);
        var blockers = new List<AnnualClosingBlocker>();
        if (PolishBusinessTime.GetDate(nowUtc) <= new DateOnly(taxYear, 12, 31))
            blockers.Add(new(AnnualClosingBlockerCodes.YearNotEnded, "Rok można zamknąć dopiero po 31 grudnia.", $"/Settlements/Year?year={taxYear}"));
        if (declaration is null)
            blockers.Add(new(AnnualClosingBlockerCodes.DeclarationMissing, "Uzupełnij i potwierdź dane roczne.", $"/Settlements/Year?year={taxYear}#declaration"));
        if (profile is null)
            blockers.Add(new(AnnualClosingBlockerCodes.FilingProfileMissing, "Uzupełnij potwierdzony profil urzędowy.", "/Settings/Filings"));

        var firstMonth = company.BusinessStartDate.Year == taxYear ? company.BusinessStartDate.Month : 1;
        var months = new List<AnnualMonthSnapshot>();
        var rates = new List<decimal>();
        for (var monthNumber = firstMonth; monthNumber <= 12; monthNumber++)
        {
            var month = new DateOnly(taxYear, monthNumber, 1);
            var monthView = await monthClosingService.GetAsync(owner, companyId, month, nowUtc, cancellationToken);
            if (monthView?.LatestSettlement is null)
            {
                blockers.Add(new(AnnualClosingBlockerCodes.MonthMissing, $"Brakuje zamknięcia za {month:yyyy-MM}.", $"/Settlements/Month?year={taxYear}&month={monthNumber}"));
                continue;
            }
            if (monthView.LatestSettlement.Status != MonthSettlementStatus.Closed || monthView.Calculation is null)
            {
                blockers.Add(new(AnnualClosingBlockerCodes.MonthOpen, $"Miesiąc {month:yyyy-MM} ma otwartą korektę.", $"/Settlements/Month?year={taxYear}&month={monthNumber}"));
                continue;
            }
            if (monthView.HasInputDrift)
            {
                blockers.Add(new(AnnualClosingBlockerCodes.MonthDrift, $"Dane miesiąca {month:yyyy-MM} zmieniły się po zamknięciu.", $"/Settlements/Month?year={taxYear}&month={monthNumber}"));
                continue;
            }
            var calculation = monthView.Calculation;
            months.Add(new(month, monthView.LatestSettlement.Id, calculation.Id,
                calculation.RevenueMonth, calculation.CostsMonth, calculation.PitAdvanceDue,
                calculation.CurrentHealthIncome, calculation.HealthContribution));
            if (calculation.HealthBasis > 0m) rates.Add(calculation.HealthContribution / calculation.HealthBasis);
        }
        if (rates.Count > 1 && rates.Any(rate => Math.Abs(rate - rates[0]) > 0.000001m))
            blockers.Add(new(AnnualClosingBlockerCodes.HealthRuleMismatch,
                "W roku występują różne stawki zdrowotnej. Wymagane jest osobne sprawdzenie reguły rocznej.",
                $"/Settings/Calculations?year={taxYear}"));

        AnnualClosingValues? values = null;
        if (declaration is not null && months.Count == 13 - firstMonth && rates.Count > 0)
        {
            var revenue = months.Sum(item => item.Revenue);
            var costs = months.Sum(item => item.Costs);
            var social = await dbContext.MonthCalculations.AsNoTracking()
                .Where(item => months.Select(month => month.MonthCalculationId).Contains(item.Id))
                .SumAsync(item => item.SocialContributionsMonth, cancellationToken);
            var pitAdjustments = await dbContext.MonthCalculations.AsNoTracking()
                .Where(item => months.Select(month => month.MonthCalculationId).Contains(item.Id))
                .SumAsync(item => item.PitBaseAdjustmentMonth, cancellationToken);
            var costsAfterInventory = costs + declaration.OpeningInventory - declaration.ClosingInventory;
            var pitIncome = revenue - costsAfterInventory - social + pitAdjustments;
            var healthIncome = months.Sum(item => item.HealthIncome)
                + declaration.ClosingInventory - declaration.OpeningInventory;
            var healthMinimum = await dbContext.MonthCalculations.AsNoTracking()
                .Where(item => months.Select(month => month.MonthCalculationId).Contains(item.Id))
                .SumAsync(item => item.HealthMinimumBase, cancellationToken);
            var healthBasis = decimal.Max(healthMinimum, healthIncome);
            var healthDue = decimal.Round(healthBasis * rates[0], 2, MidpointRounding.AwayFromZero);
            var healthDueMonthly = months.Sum(item => item.HealthContributionDue);
            values = new(revenue, costs, declaration.OpeningInventory, declaration.ClosingInventory,
                costsAfterInventory, social, pitAdjustments, pitIncome, months.Sum(item => item.PitAdvanceDue),
                declaration.PitAdvancesPaid, healthIncome, healthMinimum, healthBasis, healthDue,
                healthDueMonthly, declaration.HealthContributionsPaid, healthDue - healthDueMonthly,
                healthDue - declaration.HealthContributionsPaid);
        }

        return new Assembly(company, declaration, profile, months, values,
            new(companyId, company.Name, company.Nip, taxYear,
                declaration is null ? null : ToSnapshot(declaration), values, months,
                closings.Count == 0 ? null : ToSnapshot(closings[0]), closings.Select(ToSnapshot).ToArray(), blockers));
    }

    private async Task<IReadOnlyList<JpkPkpirRow>> BuildJpkRowsAsync(
        Company company, string owner, int taxYear, CancellationToken cancellationToken)
    {
        var sales = await dbContext.SalesInvoices.AsNoTracking()
            .Where(item => item.CompanyId == company.Id && item.OwnerUserId == owner
                && item.ServiceMonth.Year == taxYear && item.Status == SalesInvoiceStatus.Issued)
            .ToListAsync(cancellationToken);
        var costs = await (from entry in dbContext.KpirEntries.AsNoTracking()
                           join booking in dbContext.CostBookings.AsNoTracking() on entry.BookingId equals booking.Id
                           join document in dbContext.SourceDocuments.AsNoTracking() on entry.SourceDocumentId equals document.Id
                           where entry.OwnerUserId == owner && booking.CompanyId == company.Id
                               && entry.Period.Year == taxYear && entry.Included
                           select new { entry, booking, document }).ToListAsync(cancellationToken);
        var missingAddress = costs.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.document.SellerAddress));
        if (missingAddress is not null)
            throw new InvalidOperationException($"Uzupełnij adres sprzedawcy na dokumencie {missingAddress.document.InvoiceNumber}.");
        return sales.Select(item => new JpkPkpirRow(
                item.IssueDate is DateOnly issueDate && issueDate.Year == taxYear
                    ? issueDate
                    : item.ServiceMonth,
                item.InvoiceNumber!, item.KsefNumber, "PL",
                company.Counterparty.Nip, company.Counterparty.Name, company.Counterparty.Address,
                item.Description, item.NetAmount, 0m))
            .Concat(costs.Select(item => new JpkPkpirRow(
                item.booking.IssueDate, item.document.InvoiceNumber!, item.document.KsefNumber,
                string.IsNullOrWhiteSpace(item.document.SellerTaxId) ? null : item.booking.SellerCountryCode,
                NormalizeTaxIdentifier(item.booking.SellerCountryCode, item.document.SellerTaxId),
                item.document.SellerName!, item.document.SellerAddress!, item.entry.Category, 0m, item.entry.Amount)))
            .OrderBy(item => item.EventDate).ThenBy(item => item.EvidenceNumber, StringComparer.Ordinal).ToArray();
    }

    private async Task EnsureAnnualCorrectionWritableAsync(string owner, Guid companyId, int year, CancellationToken ct)
    {
        var latest = await dbContext.AnnualClosings.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner && item.TaxYear == year)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(ct);
        if (latest?.Status == AnnualClosingStatus.Closed)
            throw new InvalidOperationException("Rok jest zamknięty. Najpierw rozpocznij korektę roku.");
    }

    private async Task<AnnualClosingSnapshot> ChangeJpkAsync(
        string ownerUserId, Guid annualClosingId, Action<AnnualClosingEntity> change,
        string auditAction, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var owner = ownerUserId?.Trim() ?? string.Empty;
        var target = await RequireAnnualClosingAsync(owner, annualClosingId, cancellationToken);
        var companyId = target.CompanyId;
        var taxYear = target.TaxYear;
        dbContext.ChangeTracker.Clear();
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                AnnualClosingWriteLock.UsesPostgres(dbContext)
                    ? IsolationLevel.ReadCommitted
                    : IsolationLevel.Serializable,
                cancellationToken)
            : null;
        try
        {
            await AnnualClosingWriteLock.AcquireAsync(dbContext, companyId, taxYear, cancellationToken);
            var closing = await RequireLatestAnnualClosingAsync(owner, annualClosingId, cancellationToken);
            change(closing);
            StageJpkAudit(auditAction, closing, owner, nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(closing);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new InvalidOperationException(
                "Stan rocznego JPK zmienił się w innej karcie. Odśwież stronę.", exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<AnnualClosingEntity> RequireAnnualClosingAsync(
        string owner, Guid annualClosingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (annualClosingId == Guid.Empty)
            throw new ArgumentException("Wersja zamknięcia roku jest wymagana.", nameof(annualClosingId));
        return await dbContext.AnnualClosings.Include(item => item.Months)
            .SingleOrDefaultAsync(item => item.Id == annualClosingId && item.OwnerUserId == owner,
                cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono tej wersji zamknięcia roku.");
    }

    private async Task<AnnualClosingEntity> RequireLatestAnnualClosingAsync(
        string owner, Guid annualClosingId, CancellationToken cancellationToken)
    {
        var closing = await RequireAnnualClosingAsync(owner, annualClosingId, cancellationToken);
        var newerExists = await dbContext.AnnualClosings.AsNoTracking().AnyAsync(
            item => item.CompanyId == closing.CompanyId
                && item.OwnerUserId == owner
                && item.TaxYear == closing.TaxYear
                && item.VersionNumber > closing.VersionNumber,
            cancellationToken);
        if (newerExists)
            throw new InvalidOperationException(
                "Zatwierdzić lub wysłać można wyłącznie roczny JPK z najnowszej wersji roku.");
        return closing;
    }

    private void StageJpkAudit(
        string action, AnnualClosingEntity closing, string owner, DateTimeOffset nowUtc)
        => auditTrail.Stage(new AuditRecord(action, nameof(AnnualClosingEntity),
            closing.Id.ToString(), owner, nowUtc, JsonSerializer.Serialize(new
            {
                closing.CompanyId,
                closing.TaxYear,
                closing.VersionNumber,
                closing.JpkStatus,
                closing.JpkStoredFileId,
                closing.JpkReceiptStoredFileId,
            })));

    private async Task RequireOwnedCompanyAsync(string owner, Guid companyId, int year, CancellationToken ct)
    {
        var exists = await dbContext.Companies.AsNoTracking().Include(item => item.TaxYears)
            .AnyAsync(item => item.Id == companyId && item.OwnerUserId == owner
                && item.TaxYears.Any(taxYear => taxYear.Year == year), ct);
        if (!exists) throw new KeyNotFoundException("Nie znaleziono firmy lub roku podatkowego.");
    }

    private static StoredFile CreateStoredFile(Guid id, string owner, Guid closingId, int version,
        StoredFileRecordType type, PrivateFileDescriptor descriptor, DateTimeOffset nowUtc) => new()
        {
            Id = id,
            OwnerUserId = owner,
            StorageKey = descriptor.StorageKey,
            OriginalFileName = descriptor.OriginalFileName,
            MediaType = descriptor.MediaType,
            SizeBytes = descriptor.SizeBytes,
            Sha256 = descriptor.Sha256,
            Origin = StoredFileOrigin.GeneratedArtifact,
            RecordType = type,
            RecordId = closingId,
            RecordVersion = version,
            CreatedAtUtc = nowUtc,
        };

    private static string NormalizeReceiptMediaType(string fileName, string mediaType)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => mediaType,
        };

    private static bool IsConcurrentWrite(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return true;
            if (current is PostgresException postgres
                && (postgres.SqlState == PostgresErrorCodes.SerializationFailure
                    || postgres.SqlState == PostgresErrorCodes.UniqueViolation
                    || postgres.SqlState == PostgresErrorCodes.DeadlockDetected))
                return true;
        }
        return false;
    }

    private static string CreateFingerprint(AnnualDeclaration declaration, Guid profileId,
        IReadOnlyList<AnnualMonthSnapshot> months, AnnualClosingValues values, int version)
    {
        var json = JsonSerializer.Serialize(new
        {
            declaration.Id,
            ProfileId = profileId,
            Version = version,
            Months = months.Select(item => new { item.Month, item.MonthSettlementId, item.MonthCalculationId }),
            Values = values,
            JpkGeneratorVersion,
            PdfVersion = AnnualReportPdfGenerator.Version,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static bool Matches(AnnualDeclaration declaration, SaveAnnualDeclarationCommand command)
        => declaration.OpeningInventory == decimal.Round(command.OpeningInventory, 2)
            && declaration.ClosingInventory == decimal.Round(command.ClosingInventory, 2)
            && declaration.PitAdvancesPaid == decimal.Round(command.PitAdvancesPaid, 2)
            && declaration.HealthContributionsPaid == decimal.Round(command.HealthContributionsPaid, 2)
            && declaration.IndependentVerificationConfirmed == command.IndependentVerificationConfirmed
            && declaration.EvidenceReference == command.EvidenceReference?.Trim()
            && declaration.ConfirmedOn == command.ConfirmedOn;

    private static AnnualClosingMonthInput ToDomainInput(AnnualMonthSnapshot item)
        => new(item.Month, item.MonthSettlementId, item.MonthCalculationId, item.Revenue,
            item.Costs, item.PitAdvanceDue, item.HealthIncome, item.HealthContributionDue);

    private static AnnualDeclarationSnapshot ToSnapshot(AnnualDeclaration item)
        => new(item.Id, item.TaxYear, item.VersionNumber, item.PreviousDeclarationId,
            item.OpeningInventory, item.ClosingInventory, item.PitAdvancesPaid,
            item.HealthContributionsPaid, item.EvidenceReference, item.ConfirmedOn, item.CreatedAtUtc);

    private static AnnualClosingSnapshot ToSnapshot(AnnualClosingEntity item)
        => new(item.Id, item.TaxYear, item.VersionNumber, item.PreviousClosingId, item.Status,
            item.CorrectionReason, item.DeclarationId, item.Values,
            item.Months.OrderBy(month => month.Month).Select(month => new AnnualMonthSnapshot(
                month.Month, month.MonthSettlementId, month.MonthCalculationId, month.Revenue,
                month.Costs, month.PitAdvanceDue, month.HealthIncome, month.HealthContributionDue)).ToArray(),
            item.JpkStoredFileId, item.JpkSha256, item.JpkGeneratorVersion,
            item.JpkStatus, item.JpkApprovalEvidence, item.JpkApprovedAtUtc,
            item.JpkManualSubmissionReference, item.JpkSentAtUtc,
            item.JpkReceiptStoredFileId, item.JpkOutcomeReference, item.JpkOutcomeAtUtc,
            item.PdfStoredFileId, item.PdfSha256, item.PdfGeneratorVersion,
            item.InputFingerprint, item.FinalResultConfirmed, item.CreatedAtUtc, item.ClosedAtUtc);

    private static string? NormalizeTaxIdentifier(string? countryCode, string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return null;
        var compact = new string(taxId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var country = countryCode?.Trim().ToUpperInvariant();
        return country?.Length == 2 && compact.StartsWith(country, StringComparison.Ordinal) ? compact[2..] : compact;
    }

    private static string ValidateIdentity(string ownerUserId, Guid companyId, int taxYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (companyId == Guid.Empty) throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        if (taxYear is < 2000 or > 2200) throw new ArgumentOutOfRangeException(nameof(taxYear));
        return ownerUserId.Trim();
    }

    private sealed record Assembly(
        Company Company, AnnualDeclaration? Declaration, FilingProfileVersion? Profile,
        IReadOnlyList<AnnualMonthSnapshot> Months, AnnualClosingValues? Values, AnnualClosingView View);
}
