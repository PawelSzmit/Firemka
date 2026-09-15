using Firemka.Application.Security;

namespace Firemka.Application.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        var referencedAssemblies = typeof(ITrustedDeviceService).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("Firemka.Infrastructure", referencedAssemblies);
    }
}
