namespace Firemka.Infrastructure.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")))
        {
            Skip = "Set FIREMKA_TEST_POSTGRES to run the isolated PostgreSQL integration test.";
        }
    }
}
