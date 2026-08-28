using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class OperationalEdgeFlowTests
{
    [Fact]
    [Trait("Spec", "OH-005")]
    [Trait("Spec", "OH-006")]
    public async Task Database_health_check_bounds_failure_and_handles_caller_cancellation_as_unhealthy()
    {
        var options = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=missing;Username=none;Password=secret;Timeout=30")
            .Options;
        await using var db = new PresentationDbContext(options);
        var check = new PresentationDatabaseHealthCheck(db);
        var stopwatch = Stopwatch.StartNew();
        var result = await check.CheckHealthAsync(new());
        stopwatch.Stop();
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await check.CheckHealthAsync(new(), cancellation.Token);
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, cancelled.Status);
    }

    [Fact]
    [Trait("Spec", "PF-025")]
    [Trait("Spec", "OH-011")]
    public async Task Success_and_business_failure_logs_are_structured_without_secrets_or_probe_noise()
    {
        var logs = new TestLoggerProvider();
        await using var factory = new ApiFactory(configureTestServices: services =>
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(logs));
        using var client = factory.CreateApiClient();
        await client.GetAsync("/api/v1/admin/profile");
        await client.GetAsync("/health/ready");
        await client.GetAsync("/health/ready");

        var operation = Assert.Single(logs.Entries, x =>
            x.Category.Contains(nameof(OperationLoggingFilter), StringComparison.Ordinal)
            && x.Level == Microsoft.Extensions.Logging.LogLevel.Information);
        Assert.Contains("GET", operation.Message);
        Assert.Contains("profile", operation.Message);
        Assert.Contains("404", operation.Message);
        Assert.DoesNotContain("integration-secret", operation.Message);
        Assert.DoesNotContain(logs.Entries, x =>
            x.Category.Contains("HealthCheck", StringComparison.OrdinalIgnoreCase)
            && x.Level == Microsoft.Extensions.Logging.LogLevel.Information);
    }

    [Fact]
    [Trait("Spec", "PF-026")]
    public async Task Management_exception_is_logged_without_database_credentials()
    {
        var logs = new TestLoggerProvider();
        await using var factory = new ApiFactory(configureTestServices: services =>
        {
            services.RemoveAll<DbContextOptions<PresentationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PresentationDbContext>>();
            services.RemoveAll<PresentationDbContext>();
            services.AddDbContext<PresentationDbContext>(options => options.UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=missing;Username=none;Password=do-not-log;Timeout=1"));
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(logs);
        });
        using var client = factory.CreateApiClient();
        var response = await client.GetAsync("/api/v1/admin/profile");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var failure = Assert.Single(logs.Entries, x =>
            x.Category.Contains(nameof(OperationLoggingFilter), StringComparison.Ordinal)
            && x.Level == Microsoft.Extensions.Logging.LogLevel.Error);
        Assert.DoesNotContain("do-not-log", failure.Message);
    }
}
