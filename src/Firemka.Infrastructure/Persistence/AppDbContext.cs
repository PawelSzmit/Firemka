using Firemka.Domain.Accounting;
using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Backups;
using Firemka.Domain.Charging;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Filings;
using Firemka.Domain.Invoices;
using Firemka.Domain.Notifications;
using Firemka.Domain.Payments;
using Firemka.Domain.Sales;
using Firemka.Domain.TaxYears;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Documents;
using Firemka.Infrastructure.Identity;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Ksef.Incoming;
using Firemka.Infrastructure.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    public DbSet<CostRule> CostRules => Set<CostRule>();

    public DbSet<CostBooking> CostBookings => Set<CostBooking>();

    public DbSet<KpirEntry> KpirEntries => Set<KpirEntry>();

    public DbSet<VatPurchaseEntry> VatPurchaseEntries => Set<VatPurchaseEntry>();

    public DbSet<VehicleCostPolicy> VehicleCostPolicies => Set<VehicleCostPolicy>();

    public DbSet<ChargingCsvProfile> ChargingCsvProfiles => Set<ChargingCsvProfile>();

    public DbSet<ChargingImportBatch> ChargingImportBatches => Set<ChargingImportBatch>();

    public DbSet<ChargingSession> ChargingSessions => Set<ChargingSession>();

    public DbSet<ChargingReport> ChargingReports => Set<ChargingReport>();

    public DbSet<ChargingReportSession> ChargingReportSessions => Set<ChargingReportSession>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CalculationRuleSet> CalculationRuleSets => Set<CalculationRuleSet>();

    public DbSet<MonthDeclaration> MonthDeclarations => Set<MonthDeclaration>();

    public DbSet<MonthTaxAdjustment> MonthTaxAdjustments => Set<MonthTaxAdjustment>();

    public DbSet<MonthCalculation> MonthCalculations => Set<MonthCalculation>();

    public DbSet<MonthCalculationLine> MonthCalculationLines => Set<MonthCalculationLine>();

    public DbSet<MonthSettlement> MonthSettlements => Set<MonthSettlement>();

    public DbSet<AnnualDeclaration> AnnualDeclarations => Set<AnnualDeclaration>();

    public DbSet<Firemka.Domain.AnnualClosing.AnnualClosing> AnnualClosings => Set<Firemka.Domain.AnnualClosing.AnnualClosing>();

    public DbSet<AnnualClosingMonth> AnnualClosingMonths => Set<AnnualClosingMonth>();

    public DbSet<AnnualArchiveRequest> AnnualArchiveRequests => Set<AnnualArchiveRequest>();

    public DbSet<BackupAccessToken> BackupAccessTokens => Set<BackupAccessToken>();

    public DbSet<BackupState> BackupStates => Set<BackupState>();

    public DbSet<BackupRequest> BackupRequests => Set<BackupRequest>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<InvoiceVersion> InvoiceVersions => Set<InvoiceVersion>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<SalesAutomationSettings> SalesAutomationSettings => Set<SalesAutomationSettings>();

    public DbSet<LoginAuditEvent> LoginAuditEvents => Set<LoginAuditEvent>();

    public DbSet<OwnerBootstrap> OwnerBootstraps => Set<OwnerBootstrap>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();

    public DbSet<SourceDocumentConflict> SourceDocumentConflicts => Set<SourceDocumentConflict>();

    public DbSet<FilingProfileVersion> FilingProfileVersions => Set<FilingProfileVersion>();

    public DbSet<FilingArtifact> FilingArtifacts => Set<FilingArtifact>();

    public DbSet<KsefSyncCheckpoint> KsefSyncCheckpoints => Set<KsefSyncCheckpoint>();

    public DbSet<KsefSyncRun> KsefSyncRuns => Set<KsefSyncRun>();

    public DbSet<DocumentExtractionAttempt> DocumentExtractionAttempts => Set<DocumentExtractionAttempt>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.Nip).HasMaxLength(10);
            entity.Property(item => item.Address).HasMaxLength(500);
            entity.Property(item => item.ServiceDescription).HasMaxLength(500);
            entity.HasIndex(item => item.OwnerUserId).IsUnique();
            entity.HasOne(item => item.Counterparty)
                .WithOne()
                .HasForeignKey<Counterparty>(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            ConfigureCollection(entity.HasMany(item => item.TaxYears), "_taxYears");
            ConfigureCollection(entity.HasMany(item => item.SubscriptionRates), "_subscriptionRates");
            ConfigureCollection(entity.HasMany(item => item.EnergyRates), "_energyRates");
            ConfigureCollection(entity.HasMany(item => item.VatProfiles), "_vatProfiles");
            ConfigureCollection(entity.HasMany(item => item.ZusProfiles), "_zusProfiles");
            ConfigureCollection(entity.HasMany(item => item.VehicleProfiles), "_vehicleProfiles");
        });

        builder.Entity<Counterparty>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.Nip).HasMaxLength(10);
            entity.Property(item => item.Address).HasMaxLength(500);
        });

        builder.Entity<TaxYear>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TaxationForm).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(item => new { item.CompanyId, item.Year }).IsUnique();
        });

        builder.Entity<SubscriptionRatePeriod>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NetMonthlyAmount).HasPrecision(18, 2);
            entity.Property(item => item.VatRate).HasPrecision(5, 2);
            entity.HasIndex(item => new { item.CompanyId, item.ValidFromMonth }).IsUnique();
        });

        builder.Entity<EnergyRatePeriod>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.GrossPricePerKwh).HasPrecision(18, 4);
            entity.HasIndex(item => new { item.CompanyId, item.ValidFromMonth }).IsUnique();
        });

        builder.Entity<VatProfilePeriod>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Profile).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(item => new { item.CompanyId, item.ValidFromMonth }).IsUnique();
        });

        builder.Entity<ZusProfilePeriod>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Profile).HasConversion<string>().HasMaxLength(48);
            entity.HasIndex(item => new { item.CompanyId, item.ValidFromMonth }).IsUnique();
        });

        builder.Entity<VehicleProfilePeriod>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Arrangement).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(item => new { item.CompanyId, item.ValidFromMonth }).IsUnique();
        });

        builder.Entity<CalculationRuleSet>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Ignore(item => item.Values);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.PitThreshold).HasPrecision(18, 2);
            entity.Property(item => item.PitLowerRatePercent).HasPrecision(5, 2);
            entity.Property(item => item.PitHigherRatePercent).HasPrecision(5, 2);
            entity.Property(item => item.PitReducingAmount).HasPrecision(18, 2);
            entity.Property(item => item.PitPaymentOptionThreshold).HasPrecision(18, 2);
            entity.Property(item => item.HealthRatePercent).HasPrecision(5, 2);
            entity.Property(item => item.HealthMinimumBeforeChange).HasPrecision(18, 2);
            entity.Property(item => item.HealthMinimumFromChange).HasPrecision(18, 2);
            entity.Property(item => item.OfficialSources).HasMaxLength(2_000);
            entity.Property(item => item.Trust).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.IndependentEvidenceReference).HasMaxLength(2_000);
            entity.Property(item => item.RuleFingerprint).HasMaxLength(64);
            entity.HasIndex(item => new { item.CompanyId, item.TaxYear, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousRuleSetId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CalculationRuleSet>()
                .WithMany()
                .HasForeignKey(item => item.PreviousRuleSetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthDeclaration>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.SocialContributionsDeductible).HasPrecision(18, 2);
            entity.Property(item => item.PitBaseAdjustment).HasPrecision(18, 2);
            entity.Property(item => item.HealthIncomeAdjustment).HasPrecision(18, 2);
            entity.Property(item => item.OpeningVatCarryForward).HasPrecision(18, 2);
            entity.Property(item => item.OpeningPitAdvancesDue).HasPrecision(18, 2);
            entity.Property(item => item.EvidenceReference).HasMaxLength(2_000);
            entity.Property(item => item.ValueFingerprint).HasMaxLength(64);
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousDeclarationId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthDeclaration>()
                .WithMany()
                .HasForeignKey(item => item.PreviousDeclarationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthTaxAdjustment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).HasMaxLength(2_000);
            entity.Property(item => item.EvidenceReference).HasMaxLength(2_000);
            entity.Property(item => item.ValueFingerprint).HasMaxLength(64);
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.CreatedAtUtc });
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.ValueFingerprint }).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SourceDocument>()
                .WithMany()
                .HasForeignKey(item => item.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SalesInvoice>()
                .WithMany()
                .HasForeignKey(item => item.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthCalculation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.InputFingerprint).HasMaxLength(64);
            entity.Property(item => item.Trust).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.RevenueMonth).HasPrecision(18, 2);
            entity.Property(item => item.CostsMonth).HasPrecision(18, 2);
            entity.Property(item => item.SocialContributionsMonth).HasPrecision(18, 2);
            entity.Property(item => item.PitBaseAdjustmentMonth).HasPrecision(18, 2);
            entity.Property(item => item.HealthIncomeAdjustmentMonth).HasPrecision(18, 2);
            entity.Property(item => item.RevenueYtd).HasPrecision(18, 2);
            entity.Property(item => item.CostsYtd).HasPrecision(18, 2);
            entity.Property(item => item.SocialContributionsYtd).HasPrecision(18, 2);
            entity.Property(item => item.PitAdjustmentsYtd).HasPrecision(18, 2);
            entity.Property(item => item.PitIncomeYtd).HasPrecision(18, 2);
            entity.Property(item => item.PitTaxBase).HasPrecision(18, 2);
            entity.Property(item => item.CumulativePitTax).HasPrecision(18, 2);
            entity.Property(item => item.PriorPitAdvancesDue).HasPrecision(18, 2);
            entity.Property(item => item.PitAdvanceDue).HasPrecision(18, 2);
            entity.Property(item => item.OutputVatMonth).HasPrecision(18, 2);
            entity.Property(item => item.InputVatMonth).HasPrecision(18, 2);
            entity.Property(item => item.PriorVatCarryForward).HasPrecision(18, 2);
            entity.Property(item => item.VatPayable).HasPrecision(18, 2);
            entity.Property(item => item.VatPayableRounded).HasPrecision(18, 2);
            entity.Property(item => item.VatCarryForward).HasPrecision(18, 2);
            entity.Property(item => item.CurrentHealthIncome).HasPrecision(18, 2);
            entity.Property(item => item.PreviousMonthHealthIncome).HasPrecision(18, 2);
            entity.Property(item => item.HealthMinimumBase).HasPrecision(18, 2);
            entity.Property(item => item.HealthBasis).HasPrecision(18, 2);
            entity.Property(item => item.HealthContribution).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.InputFingerprint }).IsUnique();
            entity.HasIndex(item => item.PreviousVersionId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CalculationRuleSet>()
                .WithMany()
                .HasForeignKey(item => item.RuleSetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthDeclaration>()
                .WithMany()
                .HasForeignKey(item => item.DeclarationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthCalculation>()
                .WithMany()
                .HasForeignKey(item => item.PreviousMonthCalculationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthCalculation>()
                .WithMany()
                .HasForeignKey(item => item.PreviousVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            var relationship = entity.HasMany(item => item.Lines)
                .WithOne()
                .HasForeignKey(item => item.MonthCalculationId)
                .OnDelete(DeleteBehavior.Cascade);
            relationship.Metadata.PrincipalToDependent?.SetField("_lines");
            relationship.Metadata.PrincipalToDependent?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<MonthCalculationLine>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(120);
            entity.Property(item => item.Label).HasMaxLength(500);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.SourceReference).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.MonthCalculationId, item.Sequence }).IsUnique();
        });

        builder.Entity<MonthSettlement>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.CorrectionReason).HasMaxLength(2_000);
            entity.Property(item => item.InputFingerprint).HasMaxLength(64);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousSettlementId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthSettlement>()
                .WithMany()
                .HasForeignKey(item => item.PreviousSettlementId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthCalculation>()
                .WithMany()
                .HasForeignKey(item => item.CalculationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AnnualDeclaration>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.OpeningInventory).HasPrecision(18, 2);
            entity.Property(item => item.ClosingInventory).HasPrecision(18, 2);
            entity.Property(item => item.PitAdvancesPaid).HasPrecision(18, 2);
            entity.Property(item => item.HealthContributionsPaid).HasPrecision(18, 2);
            entity.Property(item => item.EvidenceReference).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.CompanyId, item.TaxYear, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousDeclarationId).IsUnique();
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AnnualDeclaration>().WithMany().HasForeignKey(item => item.PreviousDeclarationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Firemka.Domain.AnnualClosing.AnnualClosing>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Ignore(item => item.Values);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.CorrectionReason).HasMaxLength(2_000);
            entity.Property(item => item.Revenue).HasPrecision(18, 2);
            entity.Property(item => item.CostsBeforeInventory).HasPrecision(18, 2);
            entity.Property(item => item.OpeningInventory).HasPrecision(18, 2);
            entity.Property(item => item.ClosingInventory).HasPrecision(18, 2);
            entity.Property(item => item.CostsAfterInventory).HasPrecision(18, 2);
            entity.Property(item => item.SocialContributions).HasPrecision(18, 2);
            entity.Property(item => item.PitAdjustments).HasPrecision(18, 2);
            entity.Property(item => item.PitIncome).HasPrecision(18, 2);
            entity.Property(item => item.PitAdvancesDue).HasPrecision(18, 2);
            entity.Property(item => item.PitAdvancesPaid).HasPrecision(18, 2);
            entity.Property(item => item.HealthIncome).HasPrecision(18, 2);
            entity.Property(item => item.AnnualHealthMinimumBase).HasPrecision(18, 2);
            entity.Property(item => item.AnnualHealthBasis).HasPrecision(18, 2);
            entity.Property(item => item.AnnualHealthContributionDue).HasPrecision(18, 2);
            entity.Property(item => item.HealthContributionsDueMonthly).HasPrecision(18, 2);
            entity.Property(item => item.HealthContributionsPaid).HasPrecision(18, 2);
            entity.Property(item => item.HealthSettlementDifference).HasPrecision(18, 2);
            entity.Property(item => item.HealthPaymentDifference).HasPrecision(18, 2);
            entity.Property(item => item.JpkSha256).HasMaxLength(64);
            entity.Property(item => item.JpkGeneratorVersion).HasMaxLength(64);
            entity.Property(item => item.JpkStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.JpkApprovalEvidence).HasMaxLength(2_000);
            entity.Property(item => item.JpkManualSubmissionReference).HasMaxLength(2_000);
            entity.Property(item => item.JpkOutcomeReference).HasMaxLength(2_000);
            entity.Property(item => item.PdfSha256).HasMaxLength(64);
            entity.Property(item => item.PdfGeneratorVersion).HasMaxLength(64);
            entity.Property(item => item.InputFingerprint).HasMaxLength(64);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.CompanyId, item.TaxYear, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousClosingId).IsUnique();
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AnnualDeclaration>().WithMany().HasForeignKey(item => item.DeclarationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Firemka.Domain.AnnualClosing.AnnualClosing>().WithMany().HasForeignKey(item => item.PreviousClosingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(item => item.JpkStoredFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(item => item.JpkReceiptStoredFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(item => item.PdfStoredFileId).OnDelete(DeleteBehavior.Restrict);
            var relationship = entity.HasMany(item => item.Months).WithOne().HasForeignKey(item => item.AnnualClosingId).OnDelete(DeleteBehavior.Cascade);
            relationship.Metadata.PrincipalToDependent?.SetField("_months");
            relationship.Metadata.PrincipalToDependent?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AnnualClosingMonth>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Revenue).HasPrecision(18, 2);
            entity.Property(item => item.Costs).HasPrecision(18, 2);
            entity.Property(item => item.PitAdvanceDue).HasPrecision(18, 2);
            entity.Property(item => item.HealthIncome).HasPrecision(18, 2);
            entity.Property(item => item.HealthContributionDue).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.AnnualClosingId, item.Month }).IsUnique();
            entity.HasOne<MonthSettlement>().WithMany().HasForeignKey(item => item.MonthSettlementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthCalculation>().WithMany().HasForeignKey(item => item.MonthCalculationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AnnualArchiveRequest>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.HasIndex(item => item.AnnualClosingId).IsUnique();
            entity.HasOne<Firemka.Domain.AnnualClosing.AnnualClosing>().WithMany().HasForeignKey(item => item.AnnualClosingId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BackupAccessToken>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Prefix).HasMaxLength(20);
            entity.Property(item => item.SecretSha256).HasMaxLength(64);
            entity.HasIndex(item => item.SecretSha256).IsUnique();
            entity.HasIndex(item => item.OwnerUserId)
                .IsUnique()
                .HasFilter("\"RevokedAtUtc\" IS NULL");
        });

        builder.Entity<BackupState>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.LastFileName).HasMaxLength(255);
            entity.Property(item => item.LastManifestSha256).HasMaxLength(64);
            entity.Property(item => item.LastFailureCode).HasMaxLength(120);
            entity.HasIndex(item => item.OwnerUserId).IsUnique();
        });

        builder.Entity<BackupRequest>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.HasIndex(item => item.OwnerUserId)
                .IsUnique()
                .HasFilter("\"CompletedAtUtc\" IS NULL");
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.AmountDue).HasPrecision(18, 2);
            entity.Property(item => item.AmountPaid).HasPrecision(18, 2);
            entity.Property(item => item.Source).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.ExternalIdentifier).HasMaxLength(500);
            entity.HasIndex(item => new { item.OwnerUserId, item.Kind, item.TargetId, item.TargetVersion }).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.Source, item.ExternalIdentifier }).IsUnique();
            entity.HasOne<Company>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.IssueKey).HasMaxLength(500);
            entity.Property(item => item.DeduplicationKey).HasMaxLength(600);
            entity.Property(item => item.Subject).HasMaxLength(200);
            entity.Property(item => item.Body).HasMaxLength(1_000);
            entity.Property(item => item.ActionPath).HasMaxLength(1_000);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.LastError).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.OwnerUserId, item.DeduplicationKey }).IsUnique();
        });

        builder.Entity<CostRule>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Ignore(item => item.Fingerprint);
            entity.Ignore(item => item.CanBookAutomatically);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.FingerprintKey).HasMaxLength(64);
            entity.Property(item => item.SellerKey).HasMaxLength(320);
            entity.Property(item => item.SellerCountryCode).HasMaxLength(2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.VatRate).HasPrecision(5, 2);
            entity.Property(item => item.ServiceKind).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.KpirCategory).HasMaxLength(120);
            entity.Property(item => item.VatDeductionPercent).HasPrecision(5, 2);
            entity.Property(item => item.KpirCostPercent).HasPrecision(5, 2);
            entity.Property(item => item.KpirPeriodPolicy).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.VatPeriodPolicy).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.DecisionSource).HasMaxLength(1_000);
            entity.HasIndex(item => new { item.CompanyId, item.FingerprintKey, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.FingerprintKey })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE");
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CostRule>()
                .WithMany()
                .HasForeignKey(item => item.PreviousRuleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.PreviousRuleId).IsUnique();
        });

        builder.Entity<CostBooking>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.FingerprintKey).HasMaxLength(64);
            entity.Property(item => item.SellerKey).HasMaxLength(320);
            entity.Property(item => item.SellerCountryCode).HasMaxLength(2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.VatTreatment).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.VatRate).HasPrecision(5, 2);
            entity.Property(item => item.ServiceKind).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.GrossAmount).HasPrecision(18, 2);
            entity.Property(item => item.InputVatAmount).HasPrecision(18, 2);
            entity.Property(item => item.KpirCategory).HasMaxLength(120);
            entity.Property(item => item.VatDeductionPercent).HasPrecision(5, 2);
            entity.Property(item => item.KpirCostPercent).HasPrecision(5, 2);
            entity.Property(item => item.DeductibleVatAmount).HasPrecision(18, 2);
            entity.Property(item => item.KpirAmount).HasPrecision(18, 2);
            entity.Property(item => item.Explanation).HasMaxLength(1_000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.SourceDocumentId).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.Status });
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SourceDocument>()
                .WithMany()
                .HasForeignKey(item => item.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CostRule>()
                .WithMany()
                .HasForeignKey(item => item.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<KpirEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Category).HasMaxLength(120);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.HasIndex(item => item.BookingId).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.Period });
            entity.HasOne<CostBooking>()
                .WithOne()
                .HasForeignKey<KpirEntry>(item => item.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SourceDocument>()
                .WithMany()
                .HasForeignKey(item => item.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CostRule>()
                .WithMany()
                .HasForeignKey(item => item.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VatPurchaseEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.InputVatAmount).HasPrecision(18, 2);
            entity.Property(item => item.DeductibleVatAmount).HasPrecision(18, 2);
            entity.HasIndex(item => item.BookingId).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.Period });
            entity.HasOne<CostBooking>()
                .WithOne()
                .HasForeignKey<VatPurchaseEntry>(item => item.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SourceDocument>()
                .WithMany()
                .HasForeignKey(item => item.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CostRule>()
                .WithMany()
                .HasForeignKey(item => item.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VehicleCostPolicy>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.VatDeductionPercent).HasPrecision(5, 2);
            entity.Property(item => item.KpirCostPercent).HasPrecision(5, 2);
            entity.Property(item => item.EvidenceReference).HasMaxLength(1_000);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.CompanyId, item.Kind, item.EffectiveFromMonth }).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.Kind, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousPolicyId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<VehicleCostPolicy>()
                .WithMany()
                .HasForeignKey(item => item.PreviousPolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChargingCsvProfile>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.TimestampColumn).HasMaxLength(160);
            entity.Property(item => item.EnergyWhColumn).HasMaxLength(160);
            entity.Property(item => item.DateFormat).HasMaxLength(120);
            entity.Property(item => item.TimeZoneId).HasMaxLength(120);
            entity.Property(item => item.IdentityColumn).HasMaxLength(160);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.CompanyId, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousProfileId).IsUnique();
            entity.HasIndex(item => item.CompanyId)
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE");
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ChargingCsvProfile>()
                .WithMany()
                .HasForeignKey(item => item.PreviousProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChargingImportBatch>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.FileSha256).HasMaxLength(64);
            entity.HasIndex(item => new { item.CompanyId, item.FileSha256 }).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.CompletedAtUtc });
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ChargingCsvProfile>()
                .WithMany()
                .HasForeignKey(item => item.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChargingSession>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.OriginalTimestamp).HasMaxLength(160);
            entity.Property(item => item.EnergyWh).HasPrecision(18, 3);
            entity.Property(item => item.NormalizedRowHash).HasMaxLength(64);
            entity.Property(item => item.SourceIdentity).HasMaxLength(500);
            entity.HasIndex(item => new { item.CompanyId, item.NormalizedRowHash }).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.LocalMonth, item.StartedAtUtc });
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ChargingImportBatch>()
                .WithMany()
                .HasForeignKey(item => item.FirstImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChargingReport>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.GrossRatePerKwh).HasPrecision(18, 4);
            entity.Property(item => item.TotalWh).HasPrecision(18, 3);
            entity.Property(item => item.TotalKwh).HasPrecision(18, 3);
            entity.Property(item => item.GrossCost).HasPrecision(18, 2);
            entity.Property(item => item.TaxStatus).HasMaxLength(160);
            entity.Property(item => item.InputFingerprint).HasMaxLength(64);
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.Month, item.InputFingerprint }).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EnergyRatePeriod>()
                .WithMany()
                .HasForeignKey(item => item.EnergyRatePeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ChargingReport>()
                .WithMany()
                .HasForeignKey(item => item.PreviousReportId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.PreviousReportId).IsUnique();
            var relationship = entity.HasMany(item => item.Sessions)
                .WithOne()
                .HasForeignKey(item => item.ChargingReportId)
                .OnDelete(DeleteBehavior.Cascade);
            relationship.Metadata.PrincipalToDependent?.SetField("_sessions");
            relationship.Metadata.PrincipalToDependent?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ChargingReportSession>(entity =>
        {
            entity.HasKey(item => new { item.ChargingReportId, item.ChargingSessionId });
            entity.HasOne<ChargingSession>()
                .WithMany()
                .HasForeignKey(item => item.ChargingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SourceDocument>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Origin).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.StateVersion).IsConcurrencyToken();
            entity.Property(item => item.SourceSha256).HasMaxLength(64);
            entity.Property(item => item.SourceConflictSha256).HasMaxLength(64);
            entity.Property(item => item.SourceConflictResolution).HasMaxLength(2_000);
            entity.Ignore(item => item.HasUnresolvedSourceConflict);
            entity.Property(item => item.KsefNumber).HasMaxLength(64);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(256);
            entity.Property(item => item.SellerName).HasMaxLength(300);
            entity.Property(item => item.SellerTaxId).HasMaxLength(32);
            entity.Property(item => item.SellerAddress).HasMaxLength(500);
            entity.Property(item => item.GrossAmount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.ExtractionConfidence).HasPrecision(5, 4);
            entity.Property(item => item.UnrelatedReason).HasMaxLength(1_000);
            entity.HasIndex(item => new { item.OwnerUserId, item.KsefNumber }).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.SourceSha256 });
            entity.HasIndex(item => new { item.Status, item.UpdatedAtUtc });
            entity.HasOne<StoredFile>()
                .WithMany()
                .HasForeignKey(item => item.SourceFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SourceDocumentConflict>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ConflictingSha256).HasMaxLength(64);
            entity.Property(item => item.Resolution).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.SourceDocumentId, item.DetectedAtUtc });
            entity.HasIndex(item => new { item.SourceDocumentId, item.ConflictingSha256 }).IsUnique();
            entity.HasIndex(item => item.StoredFileId).IsUnique();
            var relationship = entity.HasOne<SourceDocument>()
                .WithMany(item => item.SourceConflicts)
                .HasForeignKey(item => item.SourceDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            relationship.Metadata.PrincipalToDependent?.SetField("_sourceConflicts");
            relationship.Metadata.PrincipalToDependent?.SetPropertyAccessMode(PropertyAccessMode.Field);
            entity.HasOne<StoredFile>()
                .WithMany()
                .HasForeignKey(item => item.StoredFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FilingProfileVersion>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.FirstName).HasMaxLength(100);
            entity.Property(item => item.LastName).HasMaxLength(150);
            entity.Property(item => item.Pesel).HasMaxLength(11);
            entity.Property(item => item.TaxOfficeCode).HasMaxLength(4);
            entity.Property(item => item.ZusInsuranceTitleCode).HasMaxLength(4);
            entity.Property(item => item.Email).HasMaxLength(254);
            entity.Property(item => item.ConfirmationEvidence).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.CompanyId, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousVersionId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FilingProfileVersion>()
                .WithOne()
                .HasForeignKey<FilingProfileVersion>(item => item.PreviousVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FilingArtifact>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.InputFingerprint).HasMaxLength(64);
            entity.Property(item => item.SchemaVersion).HasMaxLength(100);
            entity.Property(item => item.GeneratorVersion).HasMaxLength(64);
            entity.Property(item => item.FileSha256).HasMaxLength(64);
            entity.Property(item => item.ApprovalEvidence).HasMaxLength(2_000);
            entity.Property(item => item.ManualSubmissionReference).HasMaxLength(2_000);
            entity.Property(item => item.OutcomeReference).HasMaxLength(2_000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.MonthSettlementId, item.Kind, item.InputFingerprint }).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.Period, item.Kind, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => item.PreviousArtifactId).IsUnique();
            entity.HasIndex(item => item.StoredFileId).IsUnique();
            entity.HasIndex(item => item.ReceiptFileId).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthSettlement>()
                .WithMany()
                .HasForeignKey(item => item.MonthSettlementId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthCalculation>()
                .WithMany()
                .HasForeignKey(item => item.CalculationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FilingProfileVersion>()
                .WithMany()
                .HasForeignKey(item => item.FilingProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FilingArtifact>()
                .WithOne()
                .HasForeignKey<FilingArtifact>(item => item.PreviousArtifactId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>()
                .WithMany()
                .HasForeignKey(item => item.StoredFileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>()
                .WithMany()
                .HasForeignKey(item => item.ReceiptFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<KsefSyncCheckpoint>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Environment).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Cursor).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.OwnerUserId, item.Environment }).IsUnique();
        });

        builder.Entity<KsefSyncRun>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Environment).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.StartedFromCursor).HasMaxLength(2_000);
            entity.Property(item => item.FinishedAtCursor).HasMaxLength(2_000);
            entity.Property(item => item.Error).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.OwnerUserId, item.StartedAtUtc });
        });

        builder.Entity<DocumentExtractionAttempt>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Engine).HasMaxLength(120);
            entity.Property(item => item.FieldsJson).HasColumnType("jsonb");
            entity.Property(item => item.Confidence).HasPrecision(5, 4);
            entity.Property(item => item.FieldConfidencesJson).HasColumnType("jsonb");
            entity.Property(item => item.Error).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.SourceDocumentId, item.AttemptNumber }).IsUnique();
            entity.HasOne<SourceDocument>()
                .WithMany()
                .HasForeignKey(item => item.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>()
                .WithMany()
                .HasForeignKey(item => item.StoredFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceVersion>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SnapshotJson).HasColumnType("jsonb");
            entity.HasIndex(item => new { item.InvoiceId, item.VersionNumber }).IsUnique();
            entity.HasOne<InvoiceVersion>()
                .WithMany()
                .HasForeignKey(item => item.PreviousVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalesInvoice>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.NetAmount).HasPrecision(18, 2);
            entity.Property(item => item.VatRate).HasPrecision(5, 2);
            entity.Property(item => item.VatAmount).HasPrecision(18, 2);
            entity.Property(item => item.GrossAmount).HasPrecision(18, 2);
            entity.Property(item => item.PendingSubscriptionNetAmount).HasPrecision(18, 2);
            entity.Property(item => item.PendingSubscriptionVatRate).HasPrecision(5, 2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(80);
            entity.Property(item => item.SessionReferenceNumber).HasMaxLength(120);
            entity.Property(item => item.SubmissionReferenceNumber).HasMaxLength(120);
            entity.Property(item => item.KsefNumber).HasMaxLength(64);
            entity.Property(item => item.RejectionReason).HasMaxLength(2_000);
            entity.Property(item => item.IdempotencyKey).HasMaxLength(120);
            entity.Property(item => item.OutgoingXml).HasColumnType("text");
            entity.Property(item => item.UpoXml).HasColumnType("text");
            entity.Property(item => item.ReturnedKsefXml).HasColumnType("text");
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.CompanyId, item.ServiceMonth }).IsUnique();
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.InvoiceNumber }).IsUnique();
            entity.HasIndex(item => item.KsefNumber).IsUnique();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalesAutomationSettings>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.HasIndex(item => item.CompanyId).IsUnique();
            entity.HasIndex(item => item.OwnerUserId).IsUnique();
            entity.HasOne<Company>()
                .WithOne()
                .HasForeignKey<SalesAutomationSettings>(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StoredFile>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.Property(item => item.StorageKey).HasMaxLength(96);
            entity.Property(item => item.OriginalFileName).HasMaxLength(255);
            entity.Property(item => item.MediaType).HasMaxLength(100);
            entity.Property(item => item.Sha256).HasMaxLength(64);
            entity.Property(item => item.Origin).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.RecordType).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(item => item.StorageKey).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.Sha256 });
            entity.HasIndex(item => new { item.RecordType, item.RecordId, item.RecordVersion });
        });

        builder.Entity<BackgroundJob>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.JobType).HasMaxLength(120);
            entity.Property(item => item.PayloadJson).HasColumnType("jsonb");
            entity.Property(item => item.IdempotencyKey).HasMaxLength(200);
            entity.Property(item => item.State).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.LeaseOwner).HasMaxLength(120);
            entity.Property(item => item.LastError).HasMaxLength(2_000);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
            entity.HasIndex(item => new { item.State, item.AvailableAtUtc, item.LeaseExpiresAtUtc });
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MessageType).HasMaxLength(120);
            entity.Property(item => item.PayloadJson).HasColumnType("jsonb");
            entity.Property(item => item.IdempotencyKey).HasMaxLength(200);
            entity.Property(item => item.LastError).HasMaxLength(2_000);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
            entity.HasIndex(item => new { item.ProcessedAtUtc, item.AvailableAtUtc });
        });

        builder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(120);
            entity.Property(item => item.EntityType).HasMaxLength(120);
            entity.Property(item => item.EntityId).HasMaxLength(120);
            entity.Property(item => item.Actor).HasMaxLength(120);
            entity.Property(item => item.DetailsJson).HasColumnType("jsonb");
            entity.HasIndex(item => item.OccurredAtUtc);
            entity.HasIndex(item => new { item.EntityType, item.EntityId, item.OccurredAtUtc });
        });

        builder.Entity<OwnerBootstrap>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450);
            entity.HasData(new OwnerBootstrap { Id = OwnerBootstrap.SingletonId });
        });

        builder.Entity<TrustedDevice>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.Property(item => item.DisplayName).HasMaxLength(120);
            entity.HasIndex(item => new { item.UserId, item.TokenHash }).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.ExpiresAtUtc });
        });

        builder.Entity<LoginAuditEvent>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.Property(item => item.Outcome).HasMaxLength(32);
            entity.Property(item => item.Method).HasMaxLength(32);
            entity.Property(item => item.IpAddress).HasMaxLength(64);
            entity.HasIndex(item => item.OccurredAtUtc);
            entity.HasIndex(item => new { item.UserId, item.OccurredAtUtc });
        });
    }

    private static void ConfigureCollection<TChild>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.CollectionNavigationBuilder<Company, TChild> relationship,
        string fieldName)
        where TChild : class
    {
        var configuredRelationship = relationship
            .WithOne()
            .HasForeignKey("CompanyId")
            .OnDelete(DeleteBehavior.Cascade);
        configuredRelationship.Metadata.PrincipalToDependent?.SetField(fieldName);
        configuredRelationship.Metadata.PrincipalToDependent?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
