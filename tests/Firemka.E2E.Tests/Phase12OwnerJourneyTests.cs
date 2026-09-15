using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Firemka.Application.Backups;
using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Application.Payments;
using Firemka.Domain.Accounting;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Payments;
using Firemka.Domain.Sales;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Backups;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Filings.Zus;
using Firemka.Infrastructure.Identity;
using Firemka.Infrastructure.Payments;
using Firemka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Firemka.E2E.Tests;

public sealed partial class Phase12OwnerJourneyTests : IDisposable
{
    private const string Email = "owner-phase12@example.test";
    private const string Password = "Very-Strong-Phase12-Password-1!";
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T08:00:00Z");
    private readonly Phase12Factory _factory = new();

    [Fact]
    public async Task Owner_goes_from_2fa_to_closed_month_filings_payment_and_verified_backup_status()
    {
        string authenticatorKey;
        using (var setupClient = CreateClient())
        {
            authenticatorKey = await EnrollOwnerAsync(setupClient);
        }

        using var client = CreateClient();
        await LoginOwnerAsync(client, authenticatorKey);

        string ownerId;
        await using (var identityScope = _factory.Services.CreateAsyncScope())
        {
            var users = identityScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ownerId = (await users.FindByEmailAsync(Email))!.Id;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = Company.Register(
                ownerId, "Syntetyczna Firma", "1010000000", "Testowy adres", September,
                "Syntetyczny Klient", "1234567890", "Adres klienta", "Usługa testowa",
                1_000m, 23m, 1m, VehicleArrangement.None, Now);
            db.Companies.Add(company);
            var sale = SalesInvoice.CreateDraft(
                company.Id, ownerId, September, company.GetSubscriptionRate(September).Id,
                company.ServiceDescription, 1_000m, 23m, Now);
            sale.PrepareForIssue(September, "FV/09/2026", false, "<Invoice />", Now);
            sale.RegisterSubmission("phase12-session", "phase12-submission", Now);
            sale.MarkIssued("1010000000-20260901-000000000000-00", "<UPO />", "<Invoice />", Now);
            db.SalesInvoices.Add(sale);
            SeedBookedCost(db, company, ownerId);
            await db.SaveChangesAsync();

            var closing = new MonthClosingService(db, new AuditTrail(db));
            var preview = await closing.GetAsync(ownerId, company.Id, September, Now);
            await closing.ConfirmRuleSetAsync(
                ownerId,
                preview!.RuleSet!.Id,
                new ConfirmCalculationRuleSetCommand(
                    true, "synthetic:phase12-independent-check", new DateOnly(2026, 9, 12)),
                Now);
            await closing.SaveDeclarationAsync(
                ownerId,
                company.Id,
                September,
                new SaveMonthDeclarationCommand(
                    0m, 0m, 0m, 0m, 0m, true, true, "synthetic:phase12-month"),
                Now);
            var closed = await closing.CloseAsync(ownerId, company.Id, September, Now);
            Assert.Equal(Firemka.Domain.Calculations.MonthSettlementStatus.Closed, closed.Status);

            var filings = CreateFilingService(db);
            await filings.SaveProfileAsync(
                ownerId,
                company.Id,
                new SaveFilingProfileCommand(
                    "Jan", "Testowy", new DateOnly(1990, 1, 1), "90010112345", "1215", "0510",
                    "owner-phase12@example.test", "synthetic:phase12-profile", new DateOnly(2026, 9, 12)),
                Now);
            var artifacts = await filings.GenerateAsync(ownerId, company.Id, September, Now);
            Assert.Equal(3, artifacts.Count);

            var payments = new PaymentService(db, new AuditTrail(db));
            var workspace = await payments.GetAsync(ownerId, company.Id, September);
            var vat = Assert.Single(workspace.Obligations, item => item.Kind == PaymentKind.Vat);
            await payments.RecordFullAsync(
                ownerId,
                company.Id,
                September,
                new RecordFullPaymentCommand(
                    vat.Kind, vat.TargetId, vat.TargetVersion, vat.AmountDue,
                    new DateOnly(2026, 10, 20), "manual:phase12-vat"),
                Now);

            var backups = new BackupService(db);
            var token = await backups.IssueTokenAsync(ownerId, Now);
            var authorization = (await backups.AuthorizeAsync(token.RawToken, Now))!;
            await backups.ReportAsync(
                authorization,
                new BackupReportCommand(
                    true, "firemka-backup-20261002T080000000Z-v1.fmbak", new string('A', 64), 1,
                    null, null, null),
                Now);
            var overview = await backups.GetOverviewAsync(ownerId, Now.AddMinutes(1));

            Assert.True(overview.IsConfigured);
            Assert.Equal("firemka-backup-20261002T080000000Z-v1.fmbak", overview.LastFileName);
            Assert.True((await payments.GetAsync(ownerId, company.Id, September))
                .Obligations.Single(item => item.Kind == PaymentKind.Vat).IsPaid);
        }

        using var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/Month", root.Headers.Location?.OriginalString);
        using var dashboard = await client.GetAsync(root.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var html = await dashboard.Content.ReadAsStringAsync();
        Assert.Contains("Mój miesiąc", html, StringComparison.Ordinal);
        Assert.Contains("Ostatnia poprawna kopia", html, StringComparison.Ordinal);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
    }

    private async Task<string> EnrollOwnerAsync(HttpClient client)
    {
        using var setup = await PostFormAsync(client, "/Setup", new Dictionary<string, string>
        {
            ["Input.Email"] = Email,
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password,
        });
        Assert.Equal(HttpStatusCode.Redirect, setup.StatusCode);
        Assert.Equal("/Setup/TwoFactor", setup.Headers.Location?.OriginalString);

        using var twoFactorPage = await client.GetAsync("/Setup/TwoFactor");
        Assert.Equal(HttpStatusCode.OK, twoFactorPage.StatusCode);

        string authenticatorKey;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = await users.FindByEmailAsync(Email);
            authenticatorKey = (await users.GetAuthenticatorKeyAsync(owner!))!;
        }
        using var twoFactor = await PostFormAsync(client, "/Setup/TwoFactor", new Dictionary<string, string>
        {
            ["Input.Code"] = CreateTotpCode(authenticatorKey),
        });
        Assert.Equal(HttpStatusCode.Redirect, twoFactor.StatusCode);
        Assert.Equal("/Setup/RecoveryCodes", twoFactor.Headers.Location?.OriginalString);
        return authenticatorKey;
    }

    private static async Task LoginOwnerAsync(HttpClient client, string authenticatorKey)
    {
        using var password = await PostFormAsync(client, "/Account/Login", new Dictionary<string, string>
        {
            ["Input.Email"] = Email,
            ["Input.Password"] = Password,
        });
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        Assert.Equal("/Account/TwoFactor", password.Headers.Location?.OriginalString);

        using var twoFactor = await PostFormAsync(client, "/Account/TwoFactor", new Dictionary<string, string>
        {
            ["Input.Code"] = CreateTotpCode(authenticatorKey),
        });
        Assert.Equal(HttpStatusCode.Redirect, twoFactor.StatusCode);
        Assert.Equal("/", twoFactor.Headers.Location?.OriginalString);
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        IDictionary<string, string> fields)
    {
        using var page = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var match = AntiforgeryRegex().Match(await page.Content.ReadAsStringAsync());
        Assert.True(match.Success);
        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups["token"].Value),
        };
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    private static string CreateTotpCode(string base32Key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in base32Key.Trim().TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits < 8) continue;
            bits -= 8;
            bytes.Add((byte)(buffer >> bits));
            buffer &= (1 << bits) - 1;
        }
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timestep);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SeedBookedCost(AppDbContext db, Company company, string ownerId)
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), ownerId, Now);
        document.AttachSource(Guid.NewGuid(), new string('B', 64), Now);
        document.ApplyExtraction(DocumentData.Empty, null, Now);
        document.ConfirmData(
            new DocumentData(
                "K/09/2026", "Testowy dostawca", "PL123", September, 123m, "PLN", "Testowy adres dostawcy"),
            Now);
        var booking = CostBooking.CreatePending(
            document.Id,
            company.Id,
            ownerId,
            CostDocumentFingerprint.Create("PL123", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai),
            123m,
            23m,
            September,
            Now);
        var calculation = booking.Book(
            null, "Pozostałe wydatki", 100m, 100m, September, September,
            "synthetic:phase12-booking", false, Now);
        document.TransitionTo(SourceDocumentStatus.Booked, Now);
        db.SourceDocuments.Add(document);
        db.CostBookings.Add(booking);
        db.KpirEntries.Add(KpirEntry.Create(
            booking.Id, document.Id, ownerId, September, "Pozostałe wydatki",
            calculation.KpirAmount, null, Now));
        db.VatPurchaseEntries.Add(VatPurchaseEntry.Create(
            booking.Id, document.Id, ownerId, September, 23m,
            calculation.DeductibleVatAmount, null, Now));
    }

    private FilingService CreateFilingService(AppDbContext db)
        => new(
            db,
            new PrivateFileStore(new PrivateFileStoreOptions
            {
                RootPath = _factory.FileRoot,
                MaximumFileSizeBytes = 5_000_000,
            }),
            new JpkV7M3Generator(),
            new JpkPkpir3Generator(),
            new ZusDraKedu227Generator(),
            new AuditTrail(db));

    public void Dispose() => _factory.Dispose();

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryRegex();

    private sealed class Phase12Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"phase12-e2e-{Guid.NewGuid():N}";
        public string FileRoot { get; } = Path.Combine(Path.GetTempPath(), $"phase12-files-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<PrivateFileStoreOptions>();
                services.RemoveAll<PrivateFileStore>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.AddSingleton(new PrivateFileStoreOptions
                {
                    RootPath = FileRoot,
                    MaximumFileSizeBytes = 5_000_000,
                });
                services.AddSingleton<PrivateFileStore>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Directory.Exists(FileRoot)) Directory.Delete(FileRoot, recursive: true);
        }
    }
}
