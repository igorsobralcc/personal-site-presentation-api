using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class InitializationFailureTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "ix_profiles_singleton_key", 409, "Profile already exists")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "other_index", 409, "Persistence conflict")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "other_fk", 409, "Persistence conflict")]
    [InlineData(PostgresErrorCodes.CheckViolation, "other_check", 500, "An error occurred while processing your request.")]
    [Trait("Spec", "PMV-004")]
    [Trait("Spec", "PR-019")]
    public async Task Only_the_singleton_index_receives_the_singleton_diagnosis(string code, string constraint, int status, string title)
    {
        var failure = new DbUpdateException("sensitive database detail", new PostgresException(
            "sensitive database detail", "ERROR", "ERROR", code, constraintName: constraint));
        await using var factory = new ApiFactory(configureTestServices: services =>
        {
            services.RemoveAll<DbContextOptions<PresentationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PresentationDbContext>>();
            services.AddDbContext<PresentationDbContext>(options => options
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new InitializationFailureInterceptor(failure)));
        });
        using var client = factory.CreateApiClient();
        var result = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        Assert.Equal((HttpStatusCode)status, result.StatusCode);
        Assert.Equal(title, (await result.JsonAsync()).GetProperty("title").GetString());
        Assert.DoesNotContain("sensitive database detail", await result.Content.ReadAsStringAsync());
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Profiles.CountAsync()));
    }
}
