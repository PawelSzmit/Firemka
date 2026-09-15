using Firemka.Domain;

namespace Firemka.Domain.Tests;

public sealed class DomainBoundaryTests
{
    [Fact]
    public void Domain_has_no_database_or_web_framework_dependencies()
    {
        var referencedAssemblies = typeof(DomainAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencedAssemblies);
        Assert.DoesNotContain("Microsoft.AspNetCore.Http", referencedAssemblies);
        Assert.DoesNotContain("Npgsql", referencedAssemblies);
    }
}
