using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class ExperienceProjectFlowTests
{
    [Fact]
    [Trait("Spec", "EX-022")]
    public async Task Experience_operations_reject_missing_targets_and_invalid_preconditions_without_state_change()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var missingId = Guid.NewGuid();
        using var missingPatch = FlowTestSupport.Patch($"/api/v1/admin/experiences/{missingId}", "{}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingPatch)).StatusCode);
        using var missingDelete = FlowTestSupport.Delete($"/api/v1/admin/experiences/{missingId}", "\"1\"");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingDelete)).StatusCode);
        using var missingRestore = FlowTestSupport.Restore($"/api/v1/admin/experiences/{missingId}", "\"1\"");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingRestore)).StatusCode);

        var created = await client.PostAsJsonAsync("/api/v1/admin/experiences", FlowTestSupport.Experience());
        var id = (await created.JsonAsync()).GetProperty("id").GetGuid();
        using var noMatch = FlowTestSupport.Patch($"/api/v1/admin/experiences/{id}", "{\"summary\":\"No\"}");
        Assert.Equal((HttpStatusCode)428, (await client.SendAsync(noMatch)).StatusCode);
        using var stale = FlowTestSupport.Patch($"/api/v1/admin/experiences/{id}", "{\"summary\":\"No\"}", "\"0\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        Assert.Equal("Summary", (await (await client.GetAsync($"/api/v1/admin/experiences/{id}")).JsonAsync())
            .GetProperty("summary").GetString());
    }

    [Fact]
    [Trait("Spec", "EX-001")]
    [Trait("Spec", "EX-005")]
    [Trait("Spec", "EX-010")]
    public async Task Experience_minimum_create_get_and_list_are_complete_and_chronological()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var older = await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(company: "Older", startDate: "2020-01-01", endDate: "2020-01-01"));
        var current = await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(company: "Current", startDate: "2024-01-01"));
        Assert.Equal(HttpStatusCode.Created, older.StatusCode);
        Assert.Equal(HttpStatusCode.Created, current.StatusCode);
        Assert.NotNull(current.Headers.Location);
        Assert.Equal("\"1\"", current.Headers.ETag?.Tag);

        var list = await (await client.GetAsync("/api/v1/admin/experiences")).JsonAsync();
        Assert.Equal("Current", list.GetProperty("items")[0].GetProperty("company").GetString());
        Assert.Empty(list.GetProperty("items")[0].GetProperty("highlights").EnumerateArray());
        Assert.Empty(list.GetProperty("items")[0].GetProperty("technologyIds").EnumerateArray());
    }

    [Theory]
    [InlineData("{\"company\":null}", "company")]
    [InlineData("{\"role\":\"   \"}", "role")]
    [InlineData("{\"summary\":null}", "summary")]
    [InlineData("{\"startDate\":null}", "startDate")]
    [InlineData("{\"endDate\":\"2023-01-01\"}", "endDate")]
    [InlineData("{\"highlights\":null}", "highlights")]
    [InlineData("{\"highlights\":[\"Same\",\"same\"]}", "highlights")]
    [InlineData("{\"technologyIds\":null}", "technologyIds")]
    [Trait("Spec", "EX-003")]
    [Trait("Spec", "EX-004")]
    [Trait("Spec", "EX-006")]
    [Trait("Spec", "EX-008")]
    public async Task Experience_rejects_invalid_aggregate_members_without_inserting_rows(
        string replacementJson, string field)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var payload = JsonSerializer.SerializeToNode(FlowTestSupport.Experience())!.AsObject();
        var replacement = JsonSerializer.Deserialize<JsonElement>(replacementJson).EnumerateObject().Single();
        payload[field] = JsonSerializer.SerializeToNode(replacement.Value);
        var response = await client.PostAsJsonAsync("/api/v1/admin/experiences", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.JsonAsync()).GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Experiences.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "EX-002")]
    [Trait("Spec", "EX-009")]
    public async Task Experience_accepts_collection_limits_and_rejects_one_inactive_reference()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var ids = await AddTechnologiesAsync(factory, 40);
        var highlights = Enumerable.Range(1, 20).Select(x => $"Highlight {x}").ToArray();
        var accepted = await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(highlights: highlights, technologyIds: ids));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(20, (await accepted.JsonAsync()).GetProperty("highlights").GetArrayLength());

        var mixed = ids.Take(39).Append(Guid.NewGuid()).ToArray();
        var rejected = await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(company: "Rejected", technologyIds: mixed));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(1, await factory.ExecuteDbAsync(db => db.Experiences.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "EX-011")]
    [Trait("Spec", "EX-012")]
    [Trait("Spec", "EX-013")]
    [Trait("Spec", "EX-016")]
    [Trait("Spec", "EX-017")]
    public async Task Experience_patch_replaces_children_atomically_and_distinguishes_public_from_hidden_changes()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var ids = await AddTechnologiesAsync(factory, 2);
        var created = await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(highlights: ["Old"], technologyIds: [ids[0]]));
        var id = (await created.JsonAsync()).GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var publicBefore = await client.GetAsync("/api/v1/presentation");
        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        using var hiddenPatch = FlowTestSupport.Patch($"/api/v1/admin/experiences/{id}",
            $"{{\"location\":\"Hidden\",\"highlights\":[\"New\"],\"technologyIds\":[\"{ids[1]}\"]}}",
            created.Headers.ETag!.Tag);
        var hidden = await client.SendAsync(hiddenPatch);
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        var hiddenBody = await hidden.JsonAsync();
        Assert.Equal("New", hiddenBody.GetProperty("highlights")[0].GetString());
        Assert.Equal(ids[1], hiddenBody.GetProperty("technologyIds")[0].GetGuid());

        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        using var cached = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        cached.Headers.TryAddWithoutValidation("If-None-Match", publicBefore.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(cached)).StatusCode);

        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        using var publicPatch = FlowTestSupport.Patch($"/api/v1/admin/experiences/{id}",
            "{\"summary\":\"Visible change\"}", hidden.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(publicPatch)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        Assert.NotEqual(publicBefore.Headers.ETag!.Tag,
            (await client.GetAsync("/api/v1/presentation")).Headers.ETag!.Tag);
    }

    [Fact]
    [Trait("Spec", "EX-018")]
    [Trait("Spec", "EX-019")]
    [Trait("Spec", "EX-020")]
    [Trait("Spec", "EX-021")]
    [Trait("Spec", "TE-012")]
    [Trait("Spec", "TE-014")]
    [Trait("Spec", "PF-017")]
    [Trait("Spec", "PF-018")]
    [Trait("Spec", "TE-011")]
    public async Task Experience_and_technology_recover_in_dependency_order()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var technology = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", ".NET");
        var created = await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(technologyIds: [technology.Id]));
        var id = (await created.JsonAsync()).GetProperty("id").GetGuid();

        using var deleteExperience = FlowTestSupport.Delete($"/api/v1/admin/experiences/{id}",
            created.Headers.ETag!.Tag);
        var deletedExperience = await client.SendAsync(deleteExperience);
        using var deleteTechnology = FlowTestSupport.Delete($"/api/v1/admin/technologies/{technology.Id}",
            technology.ETag);
        var deletedTechnology = await client.SendAsync(deleteTechnology);
        Assert.Equal(HttpStatusCode.NoContent, deletedTechnology.StatusCode);

        using var blockedRestore = FlowTestSupport.Restore($"/api/v1/admin/experiences/{id}",
            deletedExperience.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(blockedRestore)).StatusCode);
        using var restoreTechnology = FlowTestSupport.Restore(
            $"/api/v1/admin/technologies/{technology.Id}", deletedTechnology.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(restoreTechnology)).StatusCode);
        using var restoreExperience = FlowTestSupport.Restore($"/api/v1/admin/experiences/{id}",
            deletedExperience.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(restoreExperience)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PJ-001")]
    [Trait("Spec", "PJ-002")]
    [Trait("Spec", "PJ-008")]
    [Trait("Spec", "PJ-010")]
    public async Task Project_minimum_and_complete_aggregates_follow_admin_and_public_visibility_rules()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var ids = await AddTechnologiesAsync(factory, 2);
        var hidden = await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(name: "Hidden", isFeatured: false));
        var complete = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(
            name: "Complete", repositoryUrl: "https://example.com/repo",
            liveUrl: "https://example.com", technologyIds: ids,
            image: new { url = "https://example.com/image.png", alt = new string('a', 500), width = 1, height = 1 }));
        Assert.Equal(HttpStatusCode.Created, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);

        var admin = await (await client.GetAsync("/api/v1/admin/projects")).JsonAsync();
        Assert.Equal(2, admin.GetProperty("totalItems").GetInt32());
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var presentation = await (await client.GetAsync("/api/v1/presentation")).JsonAsync();
        Assert.Single(presentation.GetProperty("projects").EnumerateArray());
        Assert.Equal("Complete", presentation.GetProperty("projects")[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("http://example.com", null, true)]
    [InlineData("relative", null, true)]
    [InlineData(null, "http://example.com", true)]
    [InlineData(null, null, null)]
    [Trait("Spec", "PJ-004")]
    [Trait("Spec", "PJ-007")]
    public async Task Project_rejects_invalid_urls_and_missing_feature_flag(
        string? repositoryUrl, string? liveUrl, bool? featured)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(repositoryUrl: repositoryUrl, liveUrl: liveUrl, isFeatured: featured));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Projects.CountAsync()));
    }

    [Theory]
    [InlineData(null, "Summary")]
    [InlineData("   ", "Summary")]
    [InlineData("Project", null)]
    [InlineData("Project", "   ")]
    [Trait("Spec", "PJ-003")]
    public async Task Project_rejects_missing_required_text(string? name, string? summary)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(name: name, summary: summary));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Projects.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PJ-006")]
    public async Task Project_rejects_duplicate_empty_and_inactive_technology_identifiers()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var active = (await AddTechnologiesAsync(factory, 1))[0];
        foreach (var identifiers in new[]
                 {
                     new[] { Guid.Empty },
                     new[] { active, active },
                     new[] { active, Guid.NewGuid() },
                     Enumerable.Range(0, 41).Select(_ => Guid.NewGuid()).ToArray()
                 })
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/projects",
                FlowTestSupport.Project(technologyIds: identifiers));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Projects.CountAsync()));
    }

    [Theory]
    [InlineData("http://example.com/image.png", "Alt", 1, 1)]
    [InlineData("https://example.com/image.png", "", 1, 1)]
    [InlineData("https://example.com/image.png", "Alt", 0, 1)]
    [InlineData("https://example.com/image.png", "Alt", 1, -1)]
    [Trait("Spec", "PJ-009")]
    public async Task Project_rejects_each_invalid_image_component(string url, string alt, int width, int height)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(image: new { url, alt, width, height }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PJ-011")]
    [Trait("Spec", "PJ-012")]
    [Trait("Spec", "PJ-013")]
    [Trait("Spec", "PJ-016")]
    [Trait("Spec", "PJ-017")]
    [Trait("Spec", "PP-015")]
    public async Task Project_patch_can_publish_replace_and_clear_aggregate_parts()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var ids = await AddTechnologiesAsync(factory, 2);
        var created = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(
            technologyIds: [ids[0]], isFeatured: false,
            image: new { url = "https://example.com/old.png", alt = "Old", width = 10, height = 10 }));
        var id = (await created.JsonAsync()).GetProperty("id").GetGuid();
        using var patch = FlowTestSupport.Patch($"/api/v1/admin/projects/{id}",
            $"{{\"isFeatured\":true,\"technologyIds\":[\"{ids[1]}\"],\"image\":null}}",
            created.Headers.ETag!.Tag);
        var response = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.True(body.GetProperty("isFeatured").GetBoolean());
        Assert.False(body.TryGetProperty("image", out _));
        Assert.Equal(ids[1], body.GetProperty("technologyIds")[0].GetGuid());
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        Assert.Single((await (await client.GetAsync("/api/v1/presentation")).JsonAsync())
            .GetProperty("projects").EnumerateArray());
    }

    [Fact]
    [Trait("Spec", "PJ-018")]
    [Trait("Spec", "PJ-019")]
    [Trait("Spec", "PJ-020")]
    [Trait("Spec", "PJ-021")]
    [Trait("Spec", "PF-019")]
    public async Task Project_restore_is_blocked_until_its_deleted_technology_is_restored()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var technology = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", "PostgreSQL");
        var created = await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(technologyIds: [technology.Id]));
        var id = (await created.JsonAsync()).GetProperty("id").GetGuid();
        using var deleteProject = FlowTestSupport.Delete($"/api/v1/admin/projects/{id}",
            created.Headers.ETag!.Tag);
        var deletedProject = await client.SendAsync(deleteProject);
        using var missingPrecondition = FlowTestSupport.Delete($"/api/v1/admin/projects/{id}");
        Assert.Equal((HttpStatusCode)428, (await client.SendAsync(missingPrecondition)).StatusCode);
        using var again = FlowTestSupport.Delete($"/api/v1/admin/projects/{id}", "\"stale\"");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(again)).StatusCode);
        using var deleteTechnology = FlowTestSupport.Delete($"/api/v1/admin/technologies/{technology.Id}",
            technology.ETag);
        var deletedTechnology = await client.SendAsync(deleteTechnology);

        using var blocked = FlowTestSupport.Restore($"/api/v1/admin/projects/{id}",
            deletedProject.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(blocked)).StatusCode);
        using var restoreTechnology = FlowTestSupport.Restore(
            $"/api/v1/admin/technologies/{technology.Id}", deletedTechnology.Headers.ETag!.Tag);
        await client.SendAsync(restoreTechnology);
        using var restoreProject = FlowTestSupport.Restore($"/api/v1/admin/projects/{id}",
            deletedProject.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(restoreProject)).StatusCode);
    }

    private static async Task<Guid[]> AddTechnologiesAsync(ApiFactory factory, int count)
    {
        var values = Enumerable.Range(1, count)
            .Select(index => new Technology { Name = $"Technology {index}", NormalizedName = $"TECHNOLOGY {index}" })
            .ToArray();
        await factory.ExecuteDbAsync(async db =>
        {
            db.Technologies.AddRange(values);
            await db.SaveChangesAsync();
        });
        return values.Select(x => x.Id).ToArray();
    }
}
