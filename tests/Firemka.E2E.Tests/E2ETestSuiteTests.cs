namespace Firemka.E2E.Tests;

public sealed class E2ETestSuiteTests
{
    [Fact]
    public void Deployment_harness_is_present_for_later_browser_and_phone_checks()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "compose.yaml")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "Caddyfile")));
    }

    [Fact]
    public void Worker_suppresses_routine_database_command_logs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsPath = Path.Combine(
            repositoryRoot,
            "src",
            "Firemka.Worker",
            "appsettings.json");
        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath));

        var level = settings.RootElement
            .GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty("Microsoft.EntityFrameworkCore.Database.Command")
            .GetString();

        Assert.Equal("Warning", level);
    }

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
