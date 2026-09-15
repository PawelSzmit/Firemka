namespace Firemka.Contract.Tests;

public sealed partial class ContractTestSuiteTests
{
    [Fact]
    public void Official_contract_fixture_validator_is_present()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(
            repositoryRoot,
            "docs",
            "research",
            "contracts",
            "validate-fixtures.sh")));
    }

    [Fact]
    public void Restore_script_requires_current_application_migrator_before_final_record_count_check()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "deploy",
            "scripts",
            "restore",
            "restore-clean-instance.sh"));

        Assert.Contains("FIREMKA_WEB_DLL", script, StringComparison.Ordinal);
        Assert.DoesNotContain("if [[ -n \"${FIREMKA_WEB_DLL:-}\" ]]", script, StringComparison.Ordinal);
        var migration = script.IndexOf("dotnet \"$FIREMKA_WEB_DLL\" --migrate", StringComparison.Ordinal);
        var finalCount = script.LastIndexOf("actual=\"$(psql", StringComparison.Ordinal);
        Assert.True(migration >= 0 && finalCount > migration,
            "Migracje bieżącej aplikacji muszą być obowiązkowe i poprzedzać końcową kontrolę rekordów.");
    }

    [Fact]
    public void Mac_client_publish_does_not_rewrite_server_dependency_locks_for_one_runtime()
    {
        var repositoryRoot = FindRepositoryRoot();
        var installer = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "deploy",
            "macos",
            "install-backup-client.sh"));

        Assert.Contains("/p:FiremkaIsolatedLockDirectory=", installer, StringComparison.Ordinal);
        var sharedProperties = File.ReadAllText(Path.Combine(repositoryRoot, "Directory.Build.props"));
        Assert.Contains("$(MSBuildProjectName).packages.lock.json", sharedProperties, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_images_are_digest_pinned_and_integrations_remain_off_by_default()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "Dockerfile"));
        var caddyDockerfile = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "Caddy.Dockerfile"));
        var postgresDockerfile = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "Postgres.Dockerfile"));
        var compose = File.ReadAllText(Path.Combine(repositoryRoot, "compose.yaml"));
        var environment = File.ReadAllText(Path.Combine(repositoryRoot, ".env.example"));

        var externalDockerImages = DockerFromRegex().Matches(dockerfile)
            .Select(match => match.Value)
            .Where(line => !line.StartsWith("FROM build", StringComparison.Ordinal)
                && !line.StartsWith("FROM runtime-base", StringComparison.Ordinal));
        Assert.All(externalDockerImages,
            image => Assert.Contains("@sha256:", image, StringComparison.Ordinal));
        Assert.All(DockerFromRegex().Matches(caddyDockerfile).Select(match => match.Value),
            image => Assert.Contains("@sha256:", image, StringComparison.Ordinal));
        Assert.All(DockerFromRegex().Matches(postgresDockerfile)
            .Select(match => match.Value)
            .Where(line => !line.Equals("FROM scratch", StringComparison.Ordinal)),
            image => Assert.Contains("@sha256:", image, StringComparison.Ordinal));
        Assert.Contains("image: ${FIREMKA_POSTGRES_IMAGE", compose, StringComparison.Ordinal);
        Assert.Contains("KSEF_ALLOW_PRODUCTION=false", environment, StringComparison.Ordinal);
        Assert.Contains("KSEF_OUTGOING_ALLOW_AUTOMATION=false", environment, StringComparison.Ordinal);
        Assert.Contains("EMAIL_ENABLED=false", environment, StringComparison.Ordinal);
        Assert.Contains("image: ${FIREMKA_WEB_IMAGE", compose, StringComparison.Ordinal);
        Assert.Contains("image: ${FIREMKA_WORKER_IMAGE", compose, StringComparison.Ordinal);
        Assert.Contains("image: ${FIREMKA_CADDY_IMAGE", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_overlay_limits_application_privileges_resources_and_logs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overlayPath = Path.Combine(repositoryRoot, "deploy", "compose.production.yaml");
        Assert.True(File.Exists(overlayPath));
        var overlay = File.ReadAllText(overlayPath);

        Assert.Contains("no-new-privileges:true", overlay, StringComparison.Ordinal);
        Assert.Contains("cap_drop:", overlay, StringComparison.Ordinal);
        Assert.Contains("read_only: true", overlay, StringComparison.Ordinal);
        Assert.Contains("pids_limit:", overlay, StringComparison.Ordinal);
        Assert.Contains("max-size:", overlay, StringComparison.Ordinal);
        Assert.DoesNotContain("POSTGRES_PASSWORD:", overlay, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase12_has_read_only_checks_incident_runbooks_and_traceable_acceptance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var required = new[]
        {
            "deploy/scripts/operations/audit-vps.sh",
            "deploy/scripts/operations/monitor-health.sh",
            "deploy/scripts/verify-phase12-local.sh",
            "deploy/systemd/firemka-health.service.template",
            "deploy/systemd/firemka-health.timer",
            "docs/runbooks/release-and-rollback.md",
            "docs/runbooks/incidents.md",
            "docs/acceptance/phase12-pilot-checklist.md",
            "docs/acceptance/requirements-traceability.md",
        };
        Assert.All(required, relative => Assert.True(
            File.Exists(Path.Combine(repositoryRoot, relative)), $"Brakuje {relative}."));

        var traceability = File.ReadAllText(Path.Combine(
            repositoryRoot, "docs", "acceptance", "requirements-traceability.md"));
        foreach (var number in Enumerable.Range(1, 46))
            Assert.Contains($"| R{number} |", traceability, StringComparison.Ordinal);

        var audit = File.ReadAllText(Path.Combine(
            repositoryRoot, "deploy", "scripts", "operations", "audit-vps.sh"));
        Assert.DoesNotContain("apt upgrade", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ufw allow", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker compose up", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("*:local", audit, StringComparison.Ordinal);
        Assert.Contains("*:latest", audit, StringComparison.Ordinal);

        var localGate = File.ReadAllText(Path.Combine(
            repositoryRoot, "deploy", "scripts", "verify-phase12-local.sh"));
        Assert.Contains("FullyQualifiedName~Postgres", localGate, StringComparison.Ordinal);
        Assert.DoesNotContain("FullyQualifiedName~PostgresTests", localGate, StringComparison.Ordinal);
        Assert.Contains("--target worker", localGate, StringComparison.Ordinal);
        Assert.Contains("deploy/Caddy.Dockerfile", localGate, StringComparison.Ordinal);
        Assert.Contains("caddy validate", localGate, StringComparison.Ordinal);
        Assert.Contains("--format json", localGate, StringComparison.Ordinal);
        Assert.Contains("jq -e", localGate, StringComparison.Ordinal);
        Assert.Contains("docker scout cves", localGate, StringComparison.Ordinal);
        Assert.Contains("--only-severity critical,high", localGate, StringComparison.Ordinal);
        Assert.Contains("--exit-code", localGate, StringComparison.Ordinal);
    }

    [System.Text.RegularExpressions.GeneratedRegex("^FROM\\s+\\S+", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex DockerFromRegex();

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Firemka.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Nie znaleziono katalogu głównego repozytorium.");
    }
}
