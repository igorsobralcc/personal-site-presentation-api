using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class PublicAndHealthFlowTests
{
    [Fact]
    [Trait("Spec", "PP-008")]
    [Trait("Spec", "PP-009")]
    [Trait("Spec", "PP-010")]
    public async Task Public_tie_breakers_are_deterministic_for_every_collection()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(socialLinks:
        [
            new { label = "First", url = "https://example.com/first" },
            new { label = "Second", url = "https://example.com/second" }
        ]));
        var timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var technologyA = new Technology
        {
            Id = Guid.Parse("00000000-0000-7000-8000-000000000001"), Name = "Same", NormalizedName = "SAME-A",
            CreatedAt = timestamp, UpdatedAt = timestamp, PublicUpdatedAt = timestamp
        };
        var technologyB = new Technology
        {
            Id = Guid.Parse("00000000-0000-7000-8000-000000000002"), Name = "Same", NormalizedName = "SAME-B",
            CreatedAt = timestamp, UpdatedAt = timestamp, PublicUpdatedAt = timestamp
        };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Technologies.AddRange(technologyA, technologyB);
            db.Experiences.AddRange(
                new Experience
                {
                    Id = Guid.Parse("00000000-0000-7000-8000-000000000011"), Company = "Current",
                    Role = "Role", StartDate = new(2024, 1, 1), Summary = "Current",
                    CreatedAt = timestamp, UpdatedAt = timestamp, PublicUpdatedAt = timestamp
                },
                new Experience
                {
                    Id = Guid.Parse("00000000-0000-7000-8000-000000000012"), Company = "Ended later",
                    Role = "Role", StartDate = new(2024, 1, 1), EndDate = new(2025, 1, 1), Summary = "Ended",
                    CreatedAt = timestamp, UpdatedAt = timestamp, PublicUpdatedAt = timestamp
                },
                new Experience
                {
                    Id = Guid.Parse("00000000-0000-7000-8000-000000000013"), Company = "Ended earlier",
                    Role = "Role", StartDate = new(2024, 1, 1), EndDate = new(2024, 6, 1), Summary = "Ended",
                    CreatedAt = timestamp, UpdatedAt = timestamp, PublicUpdatedAt = timestamp
                });
            var categoryA = new SkillCategory
            {
                Id = Guid.Parse("00000000-0000-7000-8000-000000000021"), Name = "Category A",
                NormalizedName = "CATEGORY A", CreatedAt = timestamp, UpdatedAt = timestamp,
                PublicUpdatedAt = timestamp
            };
            categoryA.Skills.Add(new Skill
            {
                Id = Guid.Parse("00000000-0000-7000-8000-000000000022"), Name = "Skill B",
                NormalizedName = "SKILL B", CategoryId = categoryA.Id, CreatedAt = timestamp,
                UpdatedAt = timestamp, PublicUpdatedAt = timestamp
            });
            categoryA.Skills.Add(new Skill
            {
                Id = Guid.Parse("00000000-0000-7000-8000-000000000021"), Name = "Skill A",
                NormalizedName = "SKILL A", CategoryId = categoryA.Id, CreatedAt = timestamp,
                UpdatedAt = timestamp, PublicUpdatedAt = timestamp
            });
            db.SkillCategories.Add(categoryA);
            db.Projects.Add(new Project
            {
                Id = Guid.Parse("00000000-0000-7000-8000-000000000031"), Name = "Project",
                Summary = "Summary", IsFeatured = true, CreatedAt = timestamp, UpdatedAt = timestamp,
                PublicUpdatedAt = timestamp,
                Technologies =
                [
                    new ProjectTechnology { TechnologyId = technologyB.Id },
                    new ProjectTechnology { TechnologyId = technologyA.Id }
                ]
            });
            await db.SaveChangesAsync();
        });

        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var body = await (await client.GetAsync("/api/v1/presentation")).JsonAsync();
        Assert.Equal(new[] { "Current", "Ended later", "Ended earlier" }, body.GetProperty("experiences")
            .EnumerateArray().Select(x => x.GetProperty("company").GetString()));
        Assert.Equal(new[] { "Skill A", "Skill B" }, body.GetProperty("skillCategories")[0]
            .GetProperty("skills").EnumerateArray().Select(x => x.GetProperty("name").GetString()));
        Assert.Equal(new[] { technologyA.Id, technologyB.Id }, body.GetProperty("projects")[0]
            .GetProperty("technologies").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        Assert.Equal(new[] { "First", "Second" }, body.GetProperty("profile").GetProperty("socialLinks")
            .EnumerateArray().Select(x => x.GetProperty("label").GetString()));
    }

    [Fact]
    [Trait("Spec", "PP-019")]
    public async Task Cancelled_public_request_does_not_return_a_cacheable_success()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync("/api/v1/presentation", cancellation.Token));
    }

    [Fact]
    [Trait("Spec", "PP-001")]
    public async Task Missing_profile_dominates_other_content_and_returns_traceable_problem()
    {
        await using var factory = new ApiFactory();
        await factory.ExecuteDbAsync(async db =>
        {
            db.Projects.Add(new Project { Name = "Orphan", Summary = "Hidden", IsFeatured = true });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/v1/presentation");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.JsonAsync();
        Assert.Equal("Presentation not found", problem.GetProperty("title").GetString());
        Assert.Contains("not been initialized", problem.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    [Trait("Spec", "PP-002")]
    [Trait("Spec", "PP-020")]
    public async Task Profile_only_presentation_is_anonymous_cacheable_and_uses_empty_arrays()
    {
        await using var factory = new ApiFactory();
        using var admin = factory.CreateApiClient();
        await admin.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        using var visitor = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await visitor.GetAsync("/api/v1/presentation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.CacheControl?.MaxAge);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);
        var body = await response.JsonAsync();
        Assert.Empty(body.GetProperty("experiences").EnumerateArray());
        Assert.Empty(body.GetProperty("projects").EnumerateArray());
        Assert.Empty(body.GetProperty("skillCategories").EnumerateArray());
    }

    [Fact]
    [Trait("Spec", "PP-003")]
    [Trait("Spec", "PP-004")]
    [Trait("Spec", "PP-005")]
    [Trait("Spec", "PP-006")]
    [Trait("Spec", "PP-007")]
    public async Task Public_projection_filters_deleted_unfeatured_and_administrative_data()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            email: "public@example.com",
            socialLinks: [new { label = "Site", url = "https://example.com" }]));
        var technology = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", ".NET");
        var category = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Backend");
        await client.PostAsJsonAsync("/api/v1/admin/skills", new { name = "C#", categoryId = category.Id });
        await client.PostAsJsonAsync("/api/v1/admin/experiences", FlowTestSupport.Experience(
            location: "Secret office", highlights: ["Admin only"], technologyIds: [technology.Id]));
        await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(name: "Visible", technologyIds: [technology.Id]));
        await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(name: "Unfeatured", isFeatured: false));
        await factory.ExecuteDbAsync(async db =>
        {
            db.Experiences.Add(new Experience
            {
                Company = "Deleted", Role = "Role", StartDate = new(2020, 1, 1), Summary = "No",
                DeletedAt = DateTimeOffset.UtcNow
            });
            db.SkillCategories.Add(new SkillCategory
            {
                Name = "Deleted category", NormalizedName = "DELETED CATEGORY",
                DeletedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        });

        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var body = await (await client.GetAsync("/api/v1/presentation")).JsonAsync();
        Assert.Single(body.GetProperty("experiences").EnumerateArray());
        var experience = body.GetProperty("experiences")[0];
        Assert.False(experience.TryGetProperty("location", out _));
        Assert.False(experience.TryGetProperty("highlights", out _));
        Assert.False(experience.TryGetProperty("technologyIds", out _));
        Assert.False(experience.TryGetProperty("version", out _));
        Assert.Single(body.GetProperty("projects").EnumerateArray());
        Assert.Equal("Visible", body.GetProperty("projects")[0].GetProperty("name").GetString());
        Assert.Single(body.GetProperty("skillCategories").EnumerateArray());
        Assert.False(body.GetProperty("skillCategories")[0].TryGetProperty("normalizedName", out _));
    }

    [Fact]
    [Trait("Spec", "PP-011")]
    [Trait("Spec", "PP-012")]
    [Trait("Spec", "PP-013")]
    [Trait("Spec", "PP-014")]
    [Trait("Spec", "TE-016")]
    [Trait("Spec", "TE-017")]
    public async Task Public_etag_changes_only_for_visible_representation_changes()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var used = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", "Used");
        var unused = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", "Unused");
        await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(technologyIds: [used.Id]));
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var before = await client.GetAsync("/api/v1/presentation");

        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        using var unusedPatch = FlowTestSupport.Patch($"/api/v1/admin/technologies/{unused.Id}",
            "{\"name\":\"Unused renamed\"}", unused.ETag);
        await client.SendAsync(unusedPatch);
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        using var cached = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        cached.Headers.TryAddWithoutValidation("If-None-Match", before.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(cached)).StatusCode);

        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        using var usedPatch = FlowTestSupport.Patch($"/api/v1/admin/technologies/{used.Id}",
            "{\"name\":\"Used renamed\"}", used.ETag);
        await client.SendAsync(usedPatch);
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var after = await client.GetAsync("/api/v1/presentation");
        Assert.NotEqual(before.Headers.ETag!.Tag, after.Headers.ETag!.Tag);
    }

    [Fact]
    [Trait("Spec", "PP-018")]
    [Trait("Spec", "PF-021")]
    public async Task Public_database_failure_is_a_traceable_non_disclosing_500()
    {
        await using var factory = new ApiFactory(configureTestServices: services =>
        {
            services.RemoveAll<DbContextOptions<PresentationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PresentationDbContext>>();
            services.RemoveAll<PresentationDbContext>();
            services.AddDbContext<PresentationDbContext>(options => options.UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=missing;Username=none;Password=secret;Timeout=1"));
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/v1/presentation");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "OH-001")]
    [Trait("Spec", "OH-002")]
    [Trait("Spec", "OH-007")]
    [Trait("Spec", "OH-010")]
    public async Task Health_success_is_anonymous_minimal_and_named()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Empty((await live.JsonAsync()).GetProperty("checks").EnumerateArray());
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = await ready.JsonAsync();
        Assert.Equal("presentation_database", body.GetProperty("checks")[0].GetProperty("name").GetString());
        Assert.DoesNotContain("connection", await ready.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "OH-003")]
    [Trait("Spec", "OH-004")]
    [Trait("Spec", "OH-008")]
    [Trait("Spec", "OH-009")]
    public async Task Any_failed_or_throwing_ready_check_makes_only_readiness_unhealthy()
    {
        await using var factory = new ApiFactory(configureTestServices: services =>
        {
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration("healthy", _ =>
                    new FixedHealthCheck(HealthCheckResult.Healthy()), null, ["ready"]));
                options.Registrations.Add(new HealthCheckRegistration("throws", _ =>
                    new ThrowingHealthCheck(), null, ["ready"]));
                options.Registrations.Add(new HealthCheckRegistration("not-ready", _ =>
                    new FixedHealthCheck(HealthCheckResult.Unhealthy()), null, ["other"]));
            });
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var checks = (await ready.JsonAsync()).GetProperty("checks");
        Assert.Equal(2, checks.GetArrayLength());
        Assert.DoesNotContain(checks.EnumerateArray(), x => x.GetProperty("name").GetString() == "not-ready");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}

internal sealed class FixedHealthCheck(HealthCheckResult result) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) => Task.FromResult(result);
}

internal sealed class ThrowingHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException("Sensitive detail");
}
