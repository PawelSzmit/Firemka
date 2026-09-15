using Firemka.Application.Security;
using Firemka.Infrastructure.Accounting;
using Firemka.Infrastructure.AnnualClosing;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Backups;
using Firemka.Infrastructure.Companies;
using Firemka.Infrastructure.Charging;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Documents;
using Firemka.Infrastructure.Email;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Filings.Zus;
using Firemka.Infrastructure.Identity;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Ksef.Incoming;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Infrastructure.Ocr;
using Firemka.Infrastructure.Notifications;
using Firemka.Infrastructure.Payments;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Pdf;
using Firemka.Infrastructure.Sales;
using Firemka.Infrastructure.Security;
using Firemka.Infrastructure.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFiremkaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSharedInfrastructure(configuration);

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 14;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Login";
            options.Cookie.Name = "Firemka.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
        });

        services.AddScoped<IAuthenticationAuditService, AuthenticationAuditService>();
        services.AddScoped<Firemka.Application.Accounting.ICostAccountingService, CostAccountingService>();
        services.AddScoped<Firemka.Application.Vehicles.IVehiclePolicyService, VehiclePolicyService>();
        services.AddSingleton<Firemka.Application.Charging.IChargingCsvParser, ChargingCsvParser>();
        services.AddScoped<Firemka.Application.Charging.IHomeChargingService, HomeChargingService>();
        services.AddScoped<ITrustedDeviceService, TrustedDeviceService>();
        services.AddScoped<Firemka.Application.Onboarding.ICompanyProfileService, CompanyProfileService>();
        services.AddScoped<Firemka.Application.Month.IMonthDashboardQuery, MonthDashboardQuery>();
        services.AddScoped<Firemka.Application.MonthClosing.IMonthClosingService, MonthClosingService>();
        services.AddScoped<Firemka.Application.AnnualClosing.IAnnualClosingService, AnnualClosingService>();
        services.AddScoped<Firemka.Application.AnnualClosing.IAnnualArchiveRequestQueue, AnnualArchiveRequestQueue>();
        services.AddSingleton<Firemka.Application.AnnualClosing.IAnnualReportPdfGenerator, AnnualReportPdfGenerator>();
        services.AddScoped<Firemka.Application.Filings.IFilingService, FilingService>();
        services.AddScoped<Firemka.Application.Documents.IIncomingDocumentService, IncomingDocumentService>();
        services.AddScoped<KsefIncomingSyncService>();
        services.AddHttpClient<Firemka.Application.ExternalServices.IIncomingKsefGateway, KsefIncomingHttpGateway>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<KsefIncomingOptions>();
                client.BaseAddress = options.GetBaseUri();
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        services.AddScoped<OwnerBootstrapService>();

        return services;
    }

    public static IServiceCollection AddFiremkaWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSharedInfrastructure(configuration);
        services.AddSingleton<IDocumentTextExtractionEngine, ProcessDocumentTextExtractionEngine>();
        services.AddSingleton<Firemka.Application.ExternalServices.IDocumentExtractor, LocalDocumentExtractor>();
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobHandler, DocumentExtractionJobHandler>();
        services.AddScoped<KsefIncomingSyncService>();
        services.AddHttpClient<Firemka.Application.ExternalServices.IIncomingKsefGateway, KsefIncomingHttpGateway>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<KsefIncomingOptions>();
                client.BaseAddress = options.GetBaseUri();
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobHandler, KsefSyncJobHandler>();
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobHandler, SalesDraftJobHandler>();
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobHandler, SalesInvoiceIssueJobHandler>();
        services.AddScoped<SalesDraftScheduler>();
        services.AddScoped<Firemka.Application.Notifications.INotificationScheduler, NotificationScheduler>();
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobHandler, NotificationEmailJobHandler>();
        services.AddScoped<Firemka.Application.Notifications.IEmailSender, SmtpEmailSender>();
        return services;
    }

    private static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Firemka")
                ?? throw new InvalidOperationException(
                    "Brakuje ConnectionStrings:Firemka. Ustaw go w sekretach użytkownika lub w zmiennych środowiskowych.");

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<Firemka.Application.Auditing.IAuditTrail, AuditTrail>();
        services.AddScoped<Firemka.Application.Backups.IBackupService, BackupService>();
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddScoped<Firemka.Application.Jobs.IBackgroundJobLeaseRenewer, BackgroundJobLeaseRenewer>();
        services.AddScoped<Firemka.Application.Jobs.ITransactionalOutbox, TransactionalOutbox>();
        services.AddSingleton(new BackgroundJobRunnerOptions());
        services.AddScoped<BackgroundJobRunner>();
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<StoredFileService>();
        var privateFileStoreOptions = new PrivateFileStoreOptions();
        configuration.GetSection(PrivateFileStoreOptions.SectionName).Bind(privateFileStoreOptions);
        services.AddSingleton(privateFileStoreOptions);
        services.AddSingleton<PrivateFileStore>();
        var backupPayloadOptions = new BackupPayloadOptions();
        configuration.GetSection(BackupPayloadOptions.SectionName).Bind(backupPayloadOptions);
        backupPayloadOptions.DataProtectionKeyRingPath = configuration["DataProtection:KeyRingPath"]
            ?? backupPayloadOptions.DataProtectionKeyRingPath;
        services.AddSingleton(backupPayloadOptions);
        services.AddScoped<IBackupDatabaseSnapshotSource, PostgresBackupDatabaseSnapshotSource>();
        services.AddScoped<Firemka.Application.Backups.IBackupPayloadWriter, BackupPayloadWriter>();
        var ksefIncomingOptions = new KsefIncomingOptions();
        configuration.GetSection(KsefIncomingOptions.SectionName).Bind(ksefIncomingOptions);
        services.AddSingleton(ksefIncomingOptions);
        var ksefOutgoingOptions = new KsefOutgoingOptions();
        configuration.GetSection(KsefOutgoingOptions.SectionName).Bind(ksefOutgoingOptions);
        services.AddSingleton(ksefOutgoingOptions);
        services.AddSingleton<Firemka.Application.Sales.IFa3InvoiceGenerator, Fa3InvoiceGenerator>();
        services.AddSingleton<Firemka.Application.Filings.IJpkV7M3Generator, JpkV7M3Generator>();
        services.AddSingleton<Firemka.Application.Filings.IJpkPkpir3Generator, JpkPkpir3Generator>();
        services.AddSingleton<Firemka.Application.Filings.IZusDraKedu227Generator, ZusDraKedu227Generator>();
        services.AddScoped<Firemka.Application.Sales.IOutgoingKsefGateway, UnconfiguredOutgoingKsefGateway>();
        services.AddScoped<Firemka.Application.Sales.ISalesInvoiceService, SalesInvoiceService>();
        services.AddScoped<Firemka.Application.Payments.IPaymentService, PaymentService>();
        var emailOptions = new EmailOptions();
        configuration.GetSection(EmailOptions.SectionName).Bind(emailOptions);
        services.AddSingleton(emailOptions);

        return services;
    }
}
