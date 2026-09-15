using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Firemka.Application.Backups;
using Firemka.BackupClient;

namespace Firemka.Infrastructure.Tests;

public sealed class BackupDownloadRunnerTests
{
    private const string Token = "fmbk_test_secret";
    private const string Password = "synthetic-recovery-password-123!";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T12:00:00Z");

    [Fact]
    public async Task Daily_and_annual_plan_downloads_verifies_and_reports_both_backups()
    {
        var root = CreateTemporaryFolder();
        var manualId = Guid.NewGuid();
        var annualId = Guid.NewGuid();
        var handler = new BackupServerHandler(
            new BackupPlan(true, manualId, [new AnnualBackupPlan(annualId, 2025)]),
            await CreatePayloadAsync());
        try
        {
            using var http = new HttpClient(handler);
            var saved = await new BackupDownloadRunner(http, new FixedSecrets()).RunAsync(
                new BackupClientConfiguration(new Uri("https://firemka.test"), root), Now);

            Assert.Equal(2, saved.Count);
            Assert.Single(Directory.GetFiles(root, "firemka-backup-*.fmbak"));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "Archives"), "firemka-archive-2025-*.fmbak"));
            Assert.Empty(Directory.GetFiles(root, "*.pending-report.json", SearchOption.AllDirectories));
            Assert.Equal(2, handler.ExportCount);
            Assert.All(handler.AuthorizationHeaders, value => Assert.Equal($"Bearer {Token}", value));
            Assert.Contains(handler.Reports, item => item.Success && item.ManualRequestId == manualId);
            Assert.Contains(handler.Reports, item => item.Success && item.AnnualRequestId == annualId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_success_report_is_retried_before_plan_without_downloading_backup_again()
    {
        var root = CreateTemporaryFolder();
        var handler = new BackupServerHandler(
            new BackupPlan(true, null, []),
            await CreatePayloadAsync())
        {
            FailNextSuccessReport = true,
        };
        try
        {
            using var http = new HttpClient(handler);
            var runner = new BackupDownloadRunner(http, new FixedSecrets());
            var configuration = new BackupClientConfiguration(new Uri("https://firemka.test"), root);

            await Assert.ThrowsAsync<HttpRequestException>(() => runner.RunAsync(configuration, Now));
            Assert.Single(Directory.GetFiles(root, "firemka-backup-*.fmbak"));
            Assert.Single(Directory.GetFiles(root, "*.pending-report.json"));
            Assert.Equal(1, handler.ExportCount);

            handler.Plan = new BackupPlan(false, null, []);
            var saved = await runner.RunAsync(configuration, Now.AddHours(1));

            Assert.Empty(saved);
            Assert.Empty(Directory.GetFiles(root, "*.pending-report.json"));
            Assert.Equal(1, handler.ExportCount);
            Assert.Single(handler.Reports, item => item.Success);
            Assert.DoesNotContain(handler.Reports, item => !item.Success);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<byte[]> CreatePayloadAsync()
    {
        var content = new Dictionary<string, byte[]>
        {
            ["database/firemka.dump"] = "synthetic database"u8.ToArray(),
            ["private-files/a.xml"] = "<Invoice />"u8.ToArray(),
            ["data-protection-keys/key.xml"] = "<key />"u8.ToArray(),
        };
        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in content)
            {
                var entry = archive.CreateEntry(item.Key);
                await using var stream = entry.Open();
                await stream.WriteAsync(item.Value);
            }
            var manifest = new BackupManifest(
                1, Now, "test",
                content.Select(item => new BackupManifestEntry(
                    item.Key, item.Value.Length, Convert.ToHexString(SHA256.HashData(item.Value)))).ToArray(),
                new Dictionary<string, long> { ["Companies"] = 1 });
            var manifestEntry = archive.CreateEntry("manifest.json");
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(
                manifestStream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        return output.ToArray();
    }

    private sealed class FixedSecrets : IBackupSecretReader
    {
        public Task<string> ReadTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Token);

        public Task<string> ReadRecoveryPasswordAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Password);
    }

    private sealed class BackupServerHandler(BackupPlan plan, byte[] payload) : HttpMessageHandler
    {
        public BackupPlan Plan { get; set; } = plan;
        public bool FailNextSuccessReport { get; set; }
        public int ExportCount { get; private set; }
        public List<string> AuthorizationHeaders { get; } = [];
        public List<BackupReportCommand> Reports { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/backups/plan")
                return Json(Plan);
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/backups/export")
            {
                ExportCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload),
                };
            }
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/backups/report")
            {
                var report = JsonSerializer.Deserialize<BackupReportCommand>(
                    await request.Content!.ReadAsStringAsync(cancellationToken),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                if (report.Success && FailNextSuccessReport)
                {
                    FailNextSuccessReport = false;
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                Reports.Add(report);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T value)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    Encoding.UTF8,
                    "application/json"),
            };
    }
}
