using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.Filings;
using Firemka.Application.Time;
using Firemka.Domain.Accounting;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Firemka.Infrastructure.Filings;

public sealed class FilingService(
    AppDbContext dbContext,
    PrivateFileStore privateFileStore,
    IJpkV7M3Generator jpkV7M3Generator,
    IJpkPkpir3Generator jpkPkpir3Generator,
    IZusDraKedu227Generator zusDraGenerator,
    IAuditTrail auditTrail) : IFilingService
{
    private const string JpkV7Schema = "JPK_V7M (3) 1-0E / 2025-12-19";
    private const string JpkPkpirSchema = "JPK_PKPIR (3) 1-0 / 2024-10-30";
    private const string ZusDraSchema = "KEDU 2.27 / metryka 5.7 / 2026";
    private const string JpkV7Generator = "firemka-jpk-v7m3-r3";
    private const string JpkPkpirGenerator = "firemka-jpk-pkpir3-r1";
    private const string ZusDraGenerator = "firemka-zus-dra-kedu227-r2";

    public async Task<FilingProfileSnapshot?> GetProfileAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateIdentity(ownerUserId, companyId);
        await RequireOwnedCompanyAsync(owner, companyId, cancellationToken);
        var profile = await dbContext.FilingProfileVersions.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        return profile is null ? null : ToSnapshot(profile);
    }

    public async Task<FilingProfileSnapshot> SaveProfileAsync(
        string ownerUserId,
        Guid companyId,
        SaveFilingProfileCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var owner = ValidateIdentity(ownerUserId, companyId);
        await RequireOwnedCompanyAsync(owner, companyId, cancellationToken);
        var latest = await dbContext.FilingProfileVersions
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && Matches(latest, command))
        {
            return ToSnapshot(latest);
        }

        var version = FilingProfileVersion.Create(
            companyId,
            owner,
            (latest?.VersionNumber ?? 0) + 1,
            latest?.Id,
            command.FirstName,
            command.LastName,
            command.BirthDate,
            command.Pesel,
            command.TaxOfficeCode,
            command.ZusInsuranceTitleCode,
            command.Email,
            command.ConfirmationEvidence,
            command.ConfirmedOn,
            nowUtc,
            PolishBusinessTime.GetDate(nowUtc));
        dbContext.FilingProfileVersions.Add(version);
        auditTrail.Stage(new AuditRecord(
            "filing-profile-confirmed",
            nameof(FilingProfileVersion),
            version.Id.ToString(),
            owner,
            nowUtc,
            JsonSerializer.Serialize(new
            {
                version.CompanyId,
                version.VersionNumber,
                version.PreviousVersionId,
                version.ConfirmedOn,
            })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(version);
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.FilingProfileVersions.AsNoTracking()
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (winner is not null && Matches(winner, command))
            {
                return ToSnapshot(winner);
            }

            throw new InvalidOperationException(
                "Profil urzędowy został w tej samej chwili zmieniony w inny sposób. Odśwież stronę.",
                exception);
        }
    }

    public async Task<FilingWorkspace> GetAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateIdentity(ownerUserId, companyId);
        var period = FirstDay(month);
        await RequireOwnedCompanyAsync(owner, companyId, cancellationToken);
        var profile = await dbContext.FilingProfileVersions.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var closed = await dbContext.MonthSettlements.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == owner
                && item.Month == period)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var artifacts = await dbContext.FilingArtifacts.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == owner
                && item.Period == period)
            .OrderByDescending(item => item.VersionNumber)
            .ThenBy(item => item.Kind)
            .ToListAsync(cancellationToken);
        return new FilingWorkspace(
            companyId,
            period,
            closed?.Status == MonthSettlementStatus.Closed,
            profile is null ? null : ToSnapshot(profile),
            artifacts.Select(ToSnapshot).ToArray());
    }

    public async Task<IReadOnlyList<FilingArtifactSnapshot>> GenerateAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateIdentity(ownerUserId, companyId);
        var period = FirstDay(month);
        var storedDescriptors = new List<PrivateFileDescriptor>();
        var expectedFingerprints = new Dictionary<FilingArtifactKind, string>();
        var usesPostgres = string.Equals(
            dbContext.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                usesPostgres ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
                cancellationToken)
            : null;
        try
        {
            if (usesPostgres)
            {
                var lockKey = GenerationLockKey(companyId, period);
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({lockKey})",
                    cancellationToken);
            }

            var company = await dbContext.Companies
                .Include(item => item.Counterparty)
                .Include(item => item.VatProfiles)
                .Include(item => item.ZusProfiles)
                .SingleOrDefaultAsync(
                    item => item.Id == companyId && item.OwnerUserId == owner,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Nie znaleziono firmy.");
            var profile = await dbContext.FilingProfileVersions.AsNoTracking()
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Najpierw zapisz i potwierdź profil urzędowy w ustawieniach.");
            var settlement = await dbContext.MonthSettlements.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == owner
                    && item.Month == period)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Najpierw zamknij wybrany miesiąc.");
            if (settlement.Status != MonthSettlementStatus.Closed
                || settlement.CalculationId is not Guid calculationId
                || string.IsNullOrWhiteSpace(settlement.InputFingerprint))
            {
                throw new InvalidOperationException("Najnowsza wersja miesiąca nie jest zamknięta.");
            }

            var calculation = await dbContext.MonthCalculations.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == calculationId
                        && item.CompanyId == companyId
                        && item.OwnerUserId == owner,
                    cancellationToken)
                ?? throw new InvalidOperationException("Zamknięcie nie ma kompletnej kalkulacji.");
            if (!string.Equals(
                    calculation.InputFingerprint,
                    settlement.InputFingerprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Odcisk kalkulacji nie pasuje do zamknięcia miesiąca.");
            }

            var kinds = ResolveKinds(company, period);
            foreach (var kind in kinds)
            {
                expectedFingerprints[kind] = ArtifactInputFingerprint(
                    settlement.InputFingerprint, profile.Id, kind);
            }
            var existing = await dbContext.FilingArtifacts.AsNoTracking()
                .Where(item => item.MonthSettlementId == settlement.Id
                    && item.OwnerUserId == owner)
                .ToListAsync(cancellationToken);
            var current = existing.Where(item =>
                    item.FilingProfileVersionId == profile.Id
                    && item.SchemaVersion == SchemaVersion(item.Kind)
                    && item.GeneratorVersion == GeneratorVersion(item.Kind)
                    && expectedFingerprints.TryGetValue(item.Kind, out var fingerprint)
                    && item.InputFingerprint == fingerprint)
                .ToList();
            var generated = new List<FilingArtifact>(current);
            foreach (var kind in kinds.Where(kind => current.All(item => item.Kind != kind)))
            {
                var previous = await dbContext.FilingArtifacts.AsNoTracking()
                    .Where(item => item.CompanyId == companyId
                        && item.OwnerUserId == owner
                        && item.Period == period
                        && item.Kind == kind)
                    .OrderByDescending(item => item.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);
                var versionNumber = (previous?.VersionNumber ?? 0) + 1;
                var filingSequenceNumber = await dbContext.FilingArtifacts.AsNoTracking()
                    .CountAsync(item => item.CompanyId == companyId
                        && item.OwnerUserId == owner
                        && item.Period == period
                        && item.Kind == kind
                        && item.SentAtUtc != null,
                        cancellationToken) + 1;
                var xml = await GenerateXmlAsync(
                    kind,
                    company,
                    profile,
                    calculation,
                    period,
                    filingSequenceNumber,
                    nowUtc,
                    cancellationToken);
                ValidateXml(kind, xml);
                var artifactId = Guid.NewGuid();
                var descriptor = await privateFileStore.SaveAsync(
                    new PrivateFileUpload(
                        FileName(kind, period, versionNumber),
                        "application/xml",
                        new MemoryStream(new UTF8Encoding(false).GetBytes(xml), writable: false)),
                    cancellationToken);
                storedDescriptors.Add(descriptor);
                var storedFile = new StoredFile
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = owner,
                    StorageKey = descriptor.StorageKey,
                    OriginalFileName = descriptor.OriginalFileName,
                    MediaType = descriptor.MediaType,
                    SizeBytes = descriptor.SizeBytes,
                    Sha256 = descriptor.Sha256,
                    Origin = StoredFileOrigin.GeneratedArtifact,
                    RecordType = StoredFileRecordType.FilingArtifactVersion,
                    RecordId = artifactId,
                    RecordVersion = versionNumber,
                    CreatedAtUtc = nowUtc,
                };
                var artifact = FilingArtifact.Prepare(
                    companyId,
                    owner,
                    settlement.Id,
                    period,
                    kind,
                    versionNumber,
                    previous?.Id,
                    calculation.Id,
                    profile.Id,
                    expectedFingerprints[kind],
                    SchemaVersion(kind),
                    GeneratorVersion(kind),
                    storedFile.Id,
                    descriptor.Sha256,
                    nowUtc,
                    artifactId);
                dbContext.StoredFiles.Add(storedFile);
                dbContext.FilingArtifacts.Add(artifact);
                generated.Add(artifact);
                auditTrail.Stage(new AuditRecord(
                    "filing-artifact-prepared",
                    nameof(FilingArtifact),
                    artifact.Id.ToString(),
                    owner,
                    nowUtc,
                    JsonSerializer.Serialize(new
                    {
                        artifact.CompanyId,
                        artifact.MonthSettlementId,
                        artifact.Period,
                        artifact.Kind,
                        artifact.VersionNumber,
                        artifact.PreviousArtifactId,
                        artifact.SchemaVersion,
                        artifact.GeneratorVersion,
                        artifact.FilingProfileVersionId,
                        artifact.FileSha256,
                    })));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return generated.OrderBy(item => item.Kind).Select(ToSnapshot).ToArray();
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            await DeletePhysicalCopiesAsync(storedDescriptors);
            dbContext.ChangeTracker.Clear();
            var latestSettlementId = await dbContext.MonthSettlements.AsNoTracking()
                .Where(item => item.CompanyId == companyId
                    && item.OwnerUserId == owner
                    && item.Month == period
                    && item.Status == MonthSettlementStatus.Closed)
                .OrderByDescending(item => item.VersionNumber)
                .Select(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var allWinners = await dbContext.FilingArtifacts.AsNoTracking()
                .Where(item => item.MonthSettlementId == latestSettlementId
                    && item.OwnerUserId == owner)
                .OrderBy(item => item.Kind)
                .ToListAsync(cancellationToken);
            var winner = allWinners.Where(item =>
                    expectedFingerprints.TryGetValue(item.Kind, out var fingerprint)
                    && item.InputFingerprint == fingerprint)
                .ToList();
            if (expectedFingerprints.Count > 0 && winner.Count == expectedFingerprints.Count)
            {
                return winner.Select(ToSnapshot).ToArray();
            }

            throw new InvalidOperationException(
                "Inne żądanie tworzyło te pliki równocześnie. Odśwież stronę.",
                exception);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            await DeletePhysicalCopiesAsync(storedDescriptors);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public Task<FilingArtifactSnapshot> ApproveAsync(
        string ownerUserId,
        Guid artifactId,
        string evidence,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
        => ChangeArtifactAsync(
            ownerUserId,
            artifactId,
            artifact => artifact.Approve(evidence, nowUtc),
            "filing-artifact-approved",
            nowUtc,
            cancellationToken);

    public async Task<FilingArtifactSnapshot> RecordExportAsync(
        string ownerUserId,
        Guid artifactId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var artifact = await RequireArtifactAsync(ownerUserId, artifactId, tracking: false, cancellationToken);
        if (artifact.Status == FilingArtifactStatus.ApprovalRequired)
        {
            throw new InvalidOperationException("Najpierw zatwierdź dokładnie tę wersję pliku.");
        }

        auditTrail.Stage(new AuditRecord(
            "filing-artifact-exported",
            nameof(FilingArtifact),
            artifact.Id.ToString(),
            ownerUserId.Trim(),
            nowUtc,
            JsonSerializer.Serialize(new
            {
                artifact.Kind,
                artifact.VersionNumber,
                artifact.StoredFileId,
                ManualExport = true,
            })));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSnapshot(artifact);
    }

    public Task<FilingArtifactSnapshot> MarkSentAsync(
        string ownerUserId,
        Guid artifactId,
        string manualSubmissionReference,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
        => ChangeArtifactAsync(
            ownerUserId,
            artifactId,
            artifact => artifact.MarkSent(manualSubmissionReference, nowUtc),
            "filing-artifact-marked-sent-manually",
            nowUtc,
            cancellationToken);

    public async Task<FilingArtifactSnapshot> RecordOutcomeAsync(
        string ownerUserId,
        Guid artifactId,
        FilingSubmissionOutcome outcome,
        FilingReceiptUpload receipt,
        string reference,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var owner = ownerUserId?.Trim() ?? string.Empty;
        var artifact = await RequireArtifactAsync(owner, artifactId, tracking: true, cancellationToken);
        PrivateFileDescriptor? descriptor = null;
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            descriptor = await privateFileStore.SaveAsync(
                new PrivateFileUpload(
                    receipt.FileName,
                    NormalizeReceiptMediaType(receipt.FileName, receipt.MediaType),
                    receipt.Content),
                cancellationToken);
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
                RecordId = artifact.Id,
                RecordVersion = artifact.VersionNumber,
                CreatedAtUtc = nowUtc,
            };
            artifact.RecordOutcome(outcome, storedFile.Id, reference, nowUtc);
            dbContext.StoredFiles.Add(storedFile);
            StageArtifactAudit("filing-artifact-outcome-recorded", artifact, owner, nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ToSnapshot(artifact);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            if (descriptor is not null)
            {
                await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            var winner = await RequireArtifactAsync(owner, artifactId, tracking: false, cancellationToken);
            var winnerReceiptSha = winner.ReceiptFileId is Guid winnerReceiptId
                ? await dbContext.StoredFiles.AsNoTracking()
                    .Where(item => item.Id == winnerReceiptId && item.OwnerUserId == owner)
                    .Select(item => item.Sha256)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            var expectedStatus = outcome == FilingSubmissionOutcome.Accepted
                ? FilingArtifactStatus.Accepted
                : FilingArtifactStatus.Rejected;
            if (descriptor is not null
                && winner.Status == expectedStatus
                && string.Equals(winner.OutcomeReference, reference.Trim(), StringComparison.Ordinal)
                && string.Equals(winnerReceiptSha, descriptor.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return ToSnapshot(winner);
            }

            throw new InvalidOperationException(
                "Wynik tej wersji został w tej samej chwili zapisany inaczej. Odśwież stronę.",
                exception);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            if (descriptor is not null)
            {
                await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();

            throw;
        }
    }

    private async Task<string> GenerateXmlAsync(
        FilingArtifactKind kind,
        Company company,
        FilingProfileVersion profile,
        MonthCalculation calculation,
        DateOnly period,
        int filingSequenceNumber,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var identity = new FilingPersonIdentity(
            company.Nip,
            profile.FirstName,
            profile.LastName,
            profile.BirthDate,
            profile.Pesel,
            profile.TaxOfficeCode,
            profile.ZusInsuranceTitleCode,
            profile.Email);
        if (kind == FilingArtifactKind.ZusDraKedu227)
        {
            return zusDraGenerator.Generate(new ZusDraKedu227Input(
                identity,
                period,
                PolishBusinessTime.GetDate(nowUtc),
                filingSequenceNumber,
                calculation.HealthBasis,
                calculation.HealthContribution));
        }

        var sales = await dbContext.SalesInvoices.AsNoTracking()
            .Where(item => item.CompanyId == company.Id
                && item.OwnerUserId == profile.OwnerUserId
                && item.ServiceMonth.Year == period.Year
                && item.ServiceMonth <= period
                && item.Status == SalesInvoiceStatus.Issued)
            .OrderBy(item => item.ServiceMonth)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var costRows = await (from entry in dbContext.KpirEntries.AsNoTracking()
                              join booking in dbContext.CostBookings.AsNoTracking()
                                  on entry.BookingId equals booking.Id
                              join document in dbContext.SourceDocuments.AsNoTracking()
                                  on entry.SourceDocumentId equals document.Id
                              where entry.OwnerUserId == profile.OwnerUserId
                                  && booking.CompanyId == company.Id
                                  && entry.Period.Year == period.Year
                                  && entry.Period <= period
                                  && entry.Included
                              orderby entry.Period, entry.Id
                              select new { entry, booking, document })
            .ToListAsync(cancellationToken);

        if (kind == FilingArtifactKind.JpkPkpir3)
        {
            var missingAddress = costRows.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(item.document.SellerAddress));
            if (missingAddress is not null)
            {
                throw new InvalidOperationException(
                    $"Uzupełnij adres sprzedawcy na dokumencie {missingAddress.document.InvoiceNumber} przed utworzeniem JPK_PKPIR.");
            }

            var rows = sales.Select(item => new JpkPkpirRow(
                    item.IssueDate ?? item.ServiceMonth,
                    item.InvoiceNumber ?? throw new InvalidOperationException("Faktura sprzedaży nie ma numeru."),
                    item.KsefNumber,
                    "PL",
                    company.Counterparty.Nip,
                    company.Counterparty.Name,
                    company.Counterparty.Address,
                    item.Description,
                    item.NetAmount,
                    0m))
                .Concat(costRows.Select(item => new JpkPkpirRow(
                    item.booking.IssueDate,
                    item.document.InvoiceNumber ?? throw new InvalidOperationException("Dokument kosztowy nie ma numeru."),
                    item.document.KsefNumber,
                    string.IsNullOrWhiteSpace(item.document.SellerTaxId) ? null : item.booking.SellerCountryCode,
                    NormalizeTaxIdentifier(item.booking.SellerCountryCode, item.document.SellerTaxId),
                    item.document.SellerName ?? throw new InvalidOperationException("Dokument kosztowy nie ma sprzedawcy."),
                    item.document.SellerAddress!,
                    item.entry.Category,
                    0m,
                    item.entry.Amount)))
                .OrderBy(item => item.EventDate)
                .ThenBy(item => item.EvidenceNumber, StringComparer.Ordinal)
                .ToArray();
            EnsureTotals(
                calculation.RevenueYtd,
                rows.Sum(item => item.Revenue),
                "przychodów KPiR");
            EnsureTotals(
                calculation.CostsYtd,
                rows.Sum(item => item.OtherExpense),
                "kosztów KPiR");
            return jpkPkpir3Generator.Generate(new JpkPkpir3Input(
                identity,
                new DateOnly(period.Year, 1, 1),
                period.AddMonths(1).AddDays(-1),
                nowUtc,
                filingSequenceNumber == 1 ? 0 : 1,
                rows));
        }

        var currentSales = sales.Where(item => item.ServiceMonth == period).ToArray();
        var purchases = await (from entry in dbContext.VatPurchaseEntries.AsNoTracking()
                               join booking in dbContext.CostBookings.AsNoTracking()
                                   on entry.BookingId equals booking.Id
                               join document in dbContext.SourceDocuments.AsNoTracking()
                                   on entry.SourceDocumentId equals document.Id
                               where entry.OwnerUserId == profile.OwnerUserId
                                   && booking.CompanyId == company.Id
                                   && entry.Period == period
                                   && entry.Included
                               orderby entry.Id
                               select new { entry, booking, document })
            .ToListAsync(cancellationToken);
        var salesRows = currentSales.Select(item => new JpkVatSalesRow(
            company.Counterparty.Nip,
            company.Counterparty.Name,
            item.InvoiceNumber ?? throw new InvalidOperationException("Faktura sprzedaży nie ma numeru."),
            item.IssueDate ?? throw new InvalidOperationException("Faktura sprzedaży nie ma daty wystawienia."),
            item.ServicePeriodTo,
            item.KsefNumber,
            item.NetAmount,
            item.VatAmount)).ToArray();
        var purchaseRows = purchases.Select(item => new JpkVatPurchaseRow(
            string.IsNullOrWhiteSpace(item.document.SellerTaxId) ? null : item.booking.SellerCountryCode,
            NormalizeTaxIdentifier(item.booking.SellerCountryCode, item.document.SellerTaxId) ?? "BRAK",
            item.document.SellerName ?? throw new InvalidOperationException("Dokument kosztowy nie ma sprzedawcy."),
            item.document.InvoiceNumber ?? throw new InvalidOperationException("Dokument kosztowy nie ma numeru."),
            item.booking.IssueDate,
            PolishBusinessTime.GetDate(item.document.CreatedAtUtc) > item.booking.IssueDate
                ? PolishBusinessTime.GetDate(item.document.CreatedAtUtc)
                : item.booking.IssueDate,
            item.document.KsefNumber,
            item.booking.GrossAmount - item.booking.InputVatAmount,
            item.entry.DeductibleVatAmount)).ToArray();
        EnsureTotals(calculation.RevenueMonth, salesRows.Sum(item => item.NetAmount23), "przychodów VAT");
        EnsureTotals(calculation.OutputVatMonth, salesRows.Sum(item => item.VatAmount23), "VAT należnego");
        EnsureTotals(calculation.InputVatMonth, purchaseRows.Sum(item => item.DeductibleVatAmount), "VAT naliczonego");
        return jpkV7M3Generator.Generate(new JpkV7M3Input(
            identity,
            period,
            nowUtc,
            filingSequenceNumber == 1 ? 1 : 2,
            salesRows,
            purchaseRows,
            calculation.PriorVatCarryForward));
    }

    private static string? NormalizeTaxIdentifier(string countryCode, string? taxIdentifier)
    {
        if (string.IsNullOrWhiteSpace(taxIdentifier))
        {
            return null;
        }

        var normalized = taxIdentifier.Trim().Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.StartsWith(countryCode, StringComparison.OrdinalIgnoreCase)
            ? normalized[countryCode.Length..]
            : normalized;
    }

    private async Task<FilingArtifactSnapshot> ChangeArtifactAsync(
        string ownerUserId,
        Guid artifactId,
        Action<FilingArtifact> change,
        string auditAction,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var owner = ownerUserId?.Trim() ?? string.Empty;
        var artifact = await RequireArtifactAsync(owner, artifactId, tracking: true, cancellationToken);
        change(artifact);
        StageArtifactAudit(auditAction, artifact, owner, nowUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(artifact);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "Stan dokumentu zmienił się w innej karcie. Odśwież stronę.",
                exception);
        }
    }

    private async Task<FilingArtifact> RequireArtifactAsync(
        string ownerUserId,
        Guid artifactId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (artifactId == Guid.Empty)
        {
            throw new ArgumentException("Dokument jest wymagany.", nameof(artifactId));
        }

        var query = tracking ? dbContext.FilingArtifacts : dbContext.FilingArtifacts.AsNoTracking();
        return await query.SingleOrDefaultAsync(
            item => item.Id == artifactId && item.OwnerUserId == ownerUserId.Trim(),
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono dokumentu urzędowego.");
    }

    private static IReadOnlyList<FilingArtifactKind> ResolveKinds(Company company, DateOnly period)
    {
        if (company.GetVatProfile(period).Profile != VatProfile.ActiveMonthly)
        {
            throw new InvalidOperationException(
                "Ten profil VAT nie ma jeszcze potwierdzonego generatora. Nie tworzę częściowego kompletu plików.");
        }

        if (company.GetZusProfile(period).Profile != ZusProfile.HealthOnlyDueToEmployment)
        {
            throw new InvalidOperationException(
                "Ten profil ZUS nie ma jeszcze potwierdzonego generatora. Nie tworzę pliku zastępczego.");
        }

        return
        [
            FilingArtifactKind.JpkPkpir3,
            FilingArtifactKind.JpkV7M3,
            FilingArtifactKind.ZusDraKedu227,
        ];
    }

    private static void EnsureTotals(decimal calculationValue, decimal rowsValue, string label)
    {
        if (decimal.Round(calculationValue, 2, MidpointRounding.AwayFromZero)
            != decimal.Round(rowsValue, 2, MidpointRounding.AwayFromZero))
        {
            throw new InvalidOperationException(
                $"Nie można bezpiecznie rozpisać {label} na wiersze dokumentu. Sprawdź jawne korekty miesiąca.");
        }
    }

    private void ValidateXml(FilingArtifactKind kind, string xml)
    {
        switch (kind)
        {
            case FilingArtifactKind.JpkV7M3:
                jpkV7M3Generator.Validate(xml);
                break;
            case FilingArtifactKind.JpkPkpir3:
                jpkPkpir3Generator.Validate(xml);
                break;
            case FilingArtifactKind.ZusDraKedu227:
                zusDraGenerator.Validate(xml);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private async Task RequireOwnedCompanyAsync(
        string owner,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AsNoTracking().AnyAsync(
                item => item.Id == companyId && item.OwnerUserId == owner,
                cancellationToken))
        {
            throw new KeyNotFoundException("Nie znaleziono firmy.");
        }
    }

    private async Task DeletePhysicalCopiesAsync(IEnumerable<PrivateFileDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
        }
    }

    private void StageArtifactAudit(
        string action,
        FilingArtifact artifact,
        string owner,
        DateTimeOffset nowUtc)
        => auditTrail.Stage(new AuditRecord(
            action,
            nameof(FilingArtifact),
            artifact.Id.ToString(),
            owner,
            nowUtc,
            JsonSerializer.Serialize(new
            {
                artifact.Kind,
                artifact.VersionNumber,
                artifact.Status,
                artifact.ReceiptFileId,
                artifact.OutcomeAtUtc,
            })));

    private static bool IsConcurrentWrite(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is PostgresException postgres
                && (postgres.SqlState == PostgresErrorCodes.SerializationFailure
                    || postgres.SqlState == PostgresErrorCodes.UniqueViolation
                    || postgres.SqlState == PostgresErrorCodes.DeadlockDetected))
            {
                return true;
            }
        }

        return false;
    }

    private static string ValidateIdentity(string ownerUserId, Guid companyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        return ownerUserId.Trim();
    }

    private static DateOnly FirstDay(DateOnly month) => new(month.Year, month.Month, 1);

    private static string FileName(FilingArtifactKind kind, DateOnly period, int version)
        => $"{kind}-{period:yyyy-MM}-v{version}.xml";

    private static string SchemaVersion(FilingArtifactKind kind) => kind switch
    {
        FilingArtifactKind.JpkV7M3 => JpkV7Schema,
        FilingArtifactKind.JpkPkpir3 => JpkPkpirSchema,
        FilingArtifactKind.ZusDraKedu227 => ZusDraSchema,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string GeneratorVersion(FilingArtifactKind kind) => kind switch
    {
        FilingArtifactKind.JpkV7M3 => JpkV7Generator,
        FilingArtifactKind.JpkPkpir3 => JpkPkpirGenerator,
        FilingArtifactKind.ZusDraKedu227 => ZusDraGenerator,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ArtifactInputFingerprint(
        string settlementFingerprint,
        Guid filingProfileVersionId,
        FilingArtifactKind kind)
    {
        var input = string.Join('|',
            settlementFingerprint.ToLowerInvariant(),
            filingProfileVersionId.ToString("N"),
            kind,
            SchemaVersion(kind),
            GeneratorVersion(kind));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static long GenerationLockKey(Guid companyId, DateOnly period)
    {
        var input = Encoding.UTF8.GetBytes($"filing|{companyId:N}|{period:yyyy-MM}");
        return BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(input));
    }

    private static string NormalizeReceiptMediaType(string fileName, string mediaType)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => mediaType,
        };

    private static bool Matches(FilingProfileVersion profile, SaveFilingProfileCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FirstName)
            || string.IsNullOrWhiteSpace(command.LastName)
            || string.IsNullOrWhiteSpace(command.Pesel)
            || string.IsNullOrWhiteSpace(command.TaxOfficeCode)
            || string.IsNullOrWhiteSpace(command.ZusInsuranceTitleCode)
            || string.IsNullOrWhiteSpace(command.ConfirmationEvidence))
        {
            return false;
        }

        return profile.FirstName == command.FirstName.Trim()
            && profile.LastName == command.LastName.Trim()
            && profile.BirthDate == command.BirthDate
            && profile.Pesel == command.Pesel
            && profile.TaxOfficeCode == command.TaxOfficeCode
            && profile.ZusInsuranceTitleCode == command.ZusInsuranceTitleCode
            && profile.Email == (string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim())
            && profile.ConfirmationEvidence == command.ConfirmationEvidence.Trim()
            && profile.ConfirmedOn == command.ConfirmedOn;
    }

    private static FilingProfileSnapshot ToSnapshot(FilingProfileVersion profile)
        => new(
            profile.Id,
            profile.CompanyId,
            profile.VersionNumber,
            profile.PreviousVersionId,
            profile.FirstName,
            profile.LastName,
            profile.BirthDate,
            profile.Pesel,
            profile.TaxOfficeCode,
            profile.ZusInsuranceTitleCode,
            profile.Email,
            profile.ConfirmationEvidence,
            profile.ConfirmedOn,
            profile.CreatedAtUtc);

    private static FilingArtifactSnapshot ToSnapshot(FilingArtifact artifact)
        => new(
            artifact.Id,
            artifact.MonthSettlementId,
            artifact.Period,
            artifact.Kind,
            artifact.VersionNumber,
            artifact.PreviousArtifactId,
            artifact.CalculationId,
            artifact.FilingProfileVersionId,
            artifact.InputFingerprint,
            artifact.SchemaVersion,
            artifact.GeneratorVersion,
            artifact.StoredFileId,
            artifact.FileSha256,
            artifact.Status,
            artifact.ApprovalEvidence,
            artifact.ApprovedAtUtc,
            artifact.ManualSubmissionReference,
            artifact.SentAtUtc,
            artifact.ReceiptFileId,
            artifact.OutcomeReference,
            artifact.OutcomeAtUtc,
            artifact.CreatedAtUtc);
}
