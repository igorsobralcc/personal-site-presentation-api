using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class ApiFactory(bool failingReadiness = false, bool seedDataEnabled = false, string environment = "Development") : WebApplicationFactory<Program>
{
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly string _databaseName = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Admin:Key", "integration-secret");
        builder.UseSetting("SeedData:Enabled", seedDataEnabled.ToString());
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PresentationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PresentationDbContext>>();
            services.RemoveAll<PresentationDbContext>();
            services.AddDbContext<PresentationDbContext>(options => options.UseInMemoryDatabase(_databaseName, _root));
            if (failingReadiness)
            {
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration("presentation_database", _ => new FailingHealthCheck(), null, ["ready"]));
                });
            }
        });
    }
    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        return client;
    }
}

internal sealed class FailingHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Unhealthy("Unavailable for test."));
}
