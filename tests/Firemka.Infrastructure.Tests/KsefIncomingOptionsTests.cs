using Firemka.Application.ExternalServices;
using Firemka.Infrastructure.Ksef.Incoming;

namespace Firemka.Infrastructure.Tests;

public sealed class KsefIncomingOptionsTests
{
    [Theory]
    [InlineData(KsefEnvironment.Test, "https://api-test.ksef.mf.gov.pl/v2/")]
    [InlineData(KsefEnvironment.Demo, "https://api-demo.ksef.mf.gov.pl/v2/")]
    [InlineData(KsefEnvironment.Production, "https://api.ksef.mf.gov.pl/v2/")]
    public void Each_environment_has_an_explicit_official_endpoint(
        KsefEnvironment environment,
        string expected)
    {
        var options = new KsefIncomingOptions { Environment = environment };

        Assert.Equal(expected, options.GetBaseUri().AbsoluteUri);
    }

    [Fact]
    public void Production_is_blocked_without_a_second_explicit_switch()
    {
        var options = new KsefIncomingOptions
        {
            Enabled = true,
            Environment = KsefEnvironment.Production,
            AllowProduction = false,
        };

        var exception = Assert.Throws<InvalidOperationException>(options.ValidateForUse);

        Assert.Contains("produkcyjne", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
