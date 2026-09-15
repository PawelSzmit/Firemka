using Firemka.Application.ExternalServices;

namespace Firemka.Application.Tests;

public sealed class ExternalPortBoundaryTests
{
    [Fact]
    public void Future_external_integrations_have_application_owned_ports()
    {
        var portTypes = new[]
        {
            typeof(IKsefGateway),
            typeof(IIncomingKsefGateway),
            typeof(IFilingExporter),
            typeof(ISubmissionGateway),
            typeof(IDocumentExtractor),
            typeof(IPaymentMatcher),
        };

        Assert.All(portTypes, portType =>
        {
            Assert.True(portType.IsInterface);
            Assert.Equal("Firemka.Application", portType.Assembly.GetName().Name);
        });
    }
}
