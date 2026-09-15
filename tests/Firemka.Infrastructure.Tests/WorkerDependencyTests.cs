using Firemka.Application.Jobs;
using Firemka.Application.ExternalServices;
using Firemka.Infrastructure.Documents;
using Firemka.Infrastructure.Ksef.Incoming;
using Firemka.Infrastructure.Ocr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Infrastructure.Tests;

public sealed class WorkerDependencyTests
{
    [Fact]
    public void Worker_registers_durable_jobs_without_web_identity_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Firemka"] =
                    "Host=localhost;Database=firemka;Username=firemka;Password=synthetic",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddFiremkaWorkerInfrastructure(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBackgroundJobQueue));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBackgroundJobHandler)
            && descriptor.ImplementationType == typeof(DocumentExtractionJobHandler));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBackgroundJobHandler)
            && descriptor.ImplementationType == typeof(KsefSyncJobHandler));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDocumentExtractor));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDocumentTextExtractionEngine));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDataProtectionProvider));
    }
}
