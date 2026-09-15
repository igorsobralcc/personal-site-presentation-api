using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

[Collection("PostgreSQL")]
public sealed class PostgreSqlCompletionTests
{
    private static string ConnectionString => Environment.GetEnvironmentVariable("PRESENTATION_TEST_CONNECTION_STRING")
        ?? throw new InvalidOperationException("Required disposable PostgreSQL connection is missing.");

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var db = new PresentationDbContext(new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connection, options => options.MigrationsHistoryTable("__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options);
        await db.Database.MigrateAsync();
        await using var reset = new NpgsqlCommand("TRUNCATE presentation.profile_social_links, presentation.experience_highlights, presentation.experience_technologies, presentation.project_technologies, presentation.skills, presentation.profiles, presentation.experiences, presentation.projects, presentation.skill_categories, presentation.technologies", connection);
        await reset.ExecuteNonQueryAsync();
        return connection;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PPC-001")]
    [Trait("Spec", "PPC-002")]
    [Trait("Spec", "PPC-003")]
    public async Task Public_projection_privacy_cache_visibility_and_hidden_edits_hold_against_PostgreSQL()
    {
        await using var connection = await OpenAsync();
        await using var factory = new PostgreSqlApiFactory(ConnectionString);
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var technology = await FlowTestSupport.CreateNamedAsync(client, "/api/v1/admin/technologies", "C#");
        var category = await FlowTestSupport.CreateNamedAsync(client, "/api/v1/admin/skill-categories", "Backend");
        await client.PostAsJsonAsync("/api/v1/admin/skills", new
        {
            name = "C#",
            categoryId = category.Id
        });
        var experience = await client.PostAsJsonAsync("/api/v1/admin/experiences", FlowTestSupport.Experience(
            location: "Private location", highlights: ["Private highlight"], technologyIds: [technology.Id]));
        var experienceId = (await experience.JsonAsync()).GetProperty("id").GetGuid();
        await client.PostAsJsonAsync("/api/v1/admin/experiences", FlowTestSupport.Experience(company: "Older", startDate: "2020-01-01"));
        var project = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(technologyIds: [technology.Id]));
        var projectId = (await project.JsonAsync()).GetProperty("id").GetGuid();
        await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(name: "Hidden project", isFeatured: false));
        var before = await client.GetAsync("/api/v1/presentation");
        var body = await before.JsonAsync();
        Assert.Single(body.GetProperty("projects").EnumerateArray());
        Assert.Equal("Company", body.GetProperty("experiences")[0].GetProperty("company").GetString());
        foreach (var hidden in new[] { "location", "highlights", "technologyIds", "version", "createdAt", "updatedAt" })
        {
            Assert.False(body.GetProperty("experiences")[0].TryGetProperty(hidden, out _));
        }
        using var hiddenEdit = FlowTestSupport.Patch($"/api/v1/admin/experiences/{experienceId}",
            "{\"highlights\":[\"Changed hidden highlight\"],\"location\":\"Changed private location\"}", experience.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(hiddenEdit)).StatusCode);
        Assert.Equal(before.Headers.ETag, (await client.GetAsync("/api/v1/presentation")).Headers.ETag);
        using var cached = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        cached.Headers.TryAddWithoutValidation("If-None-Match", $"\"other\", W/{before.Headers.ETag}");
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(cached)).StatusCode);
        using var visibleEdit = FlowTestSupport.Patch($"/api/v1/admin/skill-categories/{category.Id}",
            "{\"name\":\"Server\"}", category.ETag);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(visibleEdit)).StatusCode);
        Assert.NotEqual(before.Headers.ETag, (await client.GetAsync("/api/v1/presentation")).Headers.ETag);
        await using var newest = new NpgsqlCommand("UPDATE presentation.projects SET public_updated_at = '2030-01-01T00:00:00Z' WHERE id = @id", connection);
        newest.Parameters.AddWithValue("id", projectId);
        await newest.ExecuteNonQueryAsync();
        var withNewest = await client.GetAsync("/api/v1/presentation");
        using var unfeature = FlowTestSupport.Patch($"/api/v1/admin/projects/{projectId}", "{\"isFeatured\":false}", project.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(unfeature)).StatusCode);
        var withoutNewest = await client.GetAsync("/api/v1/presentation");
        Assert.True((await withoutNewest.JsonAsync()).GetProperty("updatedAt").GetDateTimeOffset()
            < (await withNewest.JsonAsync()).GetProperty("updatedAt").GetDateTimeOffset());
        Assert.Empty((await withoutNewest.JsonAsync()).GetProperty("projects").EnumerateArray());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PCV-001")]
    [Trait("Spec", "PMV-002")]
    [Trait("Spec", "PMV-005")]
    [Trait("Spec", "PPC-003")]
    public async Task Http_edges_round_trip_storage_and_translate_extreme_pagination()
    {
        await using var connection = await OpenAsync();
        await using var factory = new PostgreSqlApiFactory(ConnectionString);
        using var client = factory.CreateApiClient();
        var url = "https://example.com/" + new string('x', 2048 - "https://example.com/".Length);
        var profile = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            socialLinks: [new { label = "Site", url }]));
        Assert.Equal(HttpStatusCode.Created, profile.StatusCode);
        var project = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(repositoryUrl: url));
        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        await using var stored = new NpgsqlCommand("SELECT repository_url FROM presentation.projects", connection);
        Assert.Equal(url, await stored.ExecuteScalarAsync());
        var id = (await project.JsonAsync()).GetProperty("id").GetGuid();
        var before = await client.GetAsync("/api/v1/presentation");
        using var noop = FlowTestSupport.Patch($"/api/v1/admin/projects/{id}", "{}", project.Headers.ETag!.ToString());
        Assert.Equal(project.Headers.ETag, (await client.SendAsync(noop)).Headers.ETag);
        using var invalid = FlowTestSupport.Patch($"/api/v1/admin/projects/{id}",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                repositoryUrl = url + "x"
            }), project.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalid)).StatusCode);
        Assert.Equal(before.Headers.ETag, (await client.GetAsync("/api/v1/presentation")).Headers.ETag);
        using var patch = FlowTestSupport.Patch($"/api/v1/admin/projects/{id}", "{\"summary\":\"Updated\",\"technologyIds\":[]}", project.Headers.ETag!.ToString());
        var patched = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        using var delete = FlowTestSupport.Delete($"/api/v1/admin/projects/{id}", patched.Headers.ETag!.ToString());
        var deleted = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var restore = FlowTestSupport.Restore($"/api/v1/admin/projects/{id}", deleted.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(restore)).StatusCode);
        foreach (var collection in new[] { "technologies", "skill-categories", "skills", "experiences", "projects" })
        {
            using var safe = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/{collection}");
            safe.Headers.Add("X-Page", "2147483647");
            safe.Headers.Add("X-Page-Size", "1");
            var response = await client.SendAsync(safe);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty((await response.JsonAsync()).GetProperty("items").EnumerateArray());
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PCV-002")]
    public async Task Disposable_runtime_and_migrator_roles_enforce_schema_and_DDL_boundaries()
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var runtime = "presentation_runtime_" + suffix;
        var migrator = "presentation_migrator_" + suffix;
        await using var setup = new NpgsqlCommand($"""
            CREATE ROLE {runtime} NOLOGIN;
            CREATE ROLE {migrator} NOLOGIN;
            CREATE SCHEMA IF NOT EXISTS blog;
            CREATE TABLE blog.presentation_isolation_probe (id integer);
            REVOKE ALL ON SCHEMA blog FROM PUBLIC;
            GRANT USAGE ON SCHEMA presentation TO {runtime}, {migrator};
            GRANT CREATE ON SCHEMA presentation TO {migrator};
            GRANT SELECT, INSERT, UPDATE, DELETE ON presentation.profiles, presentation.profile_social_links,
                presentation.experiences, presentation.experience_highlights, presentation.experience_technologies,
                presentation.projects, presentation.project_technologies, presentation.skill_categories,
                presentation.skills, presentation.technologies TO {runtime};
            GRANT SELECT, INSERT, UPDATE, DELETE ON presentation.__ef_migrations_history TO {migrator};
            SET LOCAL ROLE {runtime};
            """, connection, transaction);
        await setup.ExecuteNonQueryAsync();
        await using (var db = new PresentationDbContext(new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options))
        {
            await db.Database.UseTransactionAsync(transaction);
            db.Technologies.Add(new Technology { Name = "Role write", NormalizedName = "ROLE WRITE" });
            await db.SaveChangesAsync();
            var value = await db.Technologies.SingleAsync();
            value.Name = "Role update";
            value.NormalizedName = "ROLE UPDATE";
            value.Version++;
            await db.SaveChangesAsync();
            Assert.Equal("Role update", (await db.Technologies.AsNoTracking().SingleAsync()).Name);
            db.Technologies.Remove(value);
            await db.SaveChangesAsync();
        }
        await using var allowed = new NpgsqlCommand("SELECT count(*) FROM presentation.profiles", connection, transaction);
        Assert.Equal(0L, await allowed.ExecuteScalarAsync());
        foreach (var sql in new[]
        {
            "SELECT * FROM blog.presentation_isolation_probe",
            "CREATE TABLE presentation.forbidden_runtime_ddl (id integer)",
            "DELETE FROM presentation.__ef_migrations_history"
        })
        {
            await transaction.SaveAsync("denied");
            await using var denied = new NpgsqlCommand(sql, connection, transaction);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
            await transaction.RollbackAsync("denied");
        }
        await using var migrate = new NpgsqlCommand($"SET LOCAL ROLE {migrator}; CREATE TABLE presentation.allowed_migrator_ddl (id integer); SELECT count(*) FROM presentation.__ef_migrations_history", connection, transaction);
        await migrate.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();
    }
}
