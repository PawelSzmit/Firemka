using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Firemka.Web.Tests;

public sealed class FiremkaWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"firemka-tests-{Guid.NewGuid():N}";
    private readonly string _fileRoot = Path.Combine(
        Path.GetTempPath(),
        $"firemka-web-tests-{Guid.NewGuid():N}");
    private readonly string _environmentName;

    public FiremkaWebApplicationFactory()
        : this("Testing")
    {
    }

    private FiremkaWebApplicationFactory(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        _environmentName = environmentName;
    }

    public static FiremkaWebApplicationFactory ForEnvironment(string environmentName) => new(environmentName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<PrivateFileStoreOptions>();
            services.RemoveAll<PrivateFileStore>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddSingleton(new PrivateFileStoreOptions
            {
                RootPath = _fileRoot,
                MaximumFileSizeBytes = 20 * 1024 * 1024,
            });
            services.AddSingleton<PrivateFileStore>();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_fileRoot))
        {
            Directory.Delete(_fileRoot, recursive: true);
        }
    }
}
