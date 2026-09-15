using Firemka.Application.ExternalServices;
using Firemka.Infrastructure.Ksef.Outgoing;

namespace Firemka.Infrastructure.Tests;

public sealed class KsefOutgoingOptionsTests
{
    [Theory]
    [InlineData(KsefEnvironment.Test, "https://api-test.ksef.mf.gov.pl/v2/")]
    [InlineData(KsefEnvironment.Demo, "https://api-demo.ksef.mf.gov.pl/v2/")]
    [InlineData(KsefEnvironment.Production, "https://api.ksef.mf.gov.pl/v2/")]
    public void Each_environment_has_an_explicit_official_endpoint(
        KsefEnvironment environment,
        string expected)
    {
        Assert.Equal(expected, new KsefOutgoingOptions { Environment = environment }.GetBaseUri().AbsoluteUri);
    }

    [Fact]
    public void Outgoing_ksef_is_disabled_and_unconfigured_by_default()
    {
        var options = new KsefOutgoingOptions();

        Assert.False(options.Enabled);
        Assert.False(options.AdapterConfigured);
        Assert.False(options.AllowProduction);
        Assert.False(options.AllowAutomation);
        Assert.Throws<InvalidOperationException>(options.ValidateForUse);
    }

    [Fact]
    public void Enabling_the_switch_without_an_accepted_adapter_still_blocks_sending()
    {
        var options = new KsefOutgoingOptions { Enabled = true };

        var exception = Assert.Throws<InvalidOperationException>(options.ValidateForUse);

        Assert.Contains("adapter", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_and_automation_have_separate_operator_gates()
    {
        var options = new KsefOutgoingOptions
        {
            Enabled = true,
            AdapterConfigured = true,
            Environment = KsefEnvironment.Production,
        };

        Assert.Throws<InvalidOperationException>(options.ValidateForUse);
        options.AllowProduction = true;
        options.ValidateForUse();
        Assert.Throws<InvalidOperationException>(options.ValidateAutomationUse);
        options.AllowAutomation = true;
        options.ValidateAutomationUse();
    }
}
