using Firemka.Application.Filings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubmissionGateway = Firemka.Application.ExternalServices.ISubmissionGateway;

namespace Firemka.Infrastructure.Tests;

public sealed class FilingDependencyTests
{
    [Fact]
    public void Web_infrastructure_registers_manual_filing_service_but_no_submission_gateway()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Firemka"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["PrivateFiles:RootPath"] = Path.Combine(Path.GetTempPath(), $"firemka-filings-{Guid.NewGuid():N}"),
            })
            .Build();
        var services = new ServiceCollection();

        services.AddFiremkaInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IFilingService>());
        Assert.Null(provider.GetService<SubmissionGateway>());
        Assert.Null(typeof(IFilingService).Assembly.GetType("Firemka.Application.Filings.ISubmissionGateway"));
    }
}
