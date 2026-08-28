using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class CatalogAndSkillFlowTests
{
    public static TheoryData<string, string> NamedCollections => new()
    {
        { "/api/v1/admin/skill-categories", "SC" },
        { "/api/v1/admin/technologies", "TE" }
    };

    [Theory]
    [MemberData(nameof(NamedCollections))]
    [Trait("Spec", "SC-001")]
    [Trait("Spec", "SC-002")]
    [Trait("Spec", "SC-003")]
    [Trait("Spec", "SC-004")]
    [Trait("Spec", "TE-001")]
    [Trait("Spec", "TE-002")]
    [Trait("Spec", "TE-003")]
    public async Task Named_resources_trim_accept_boundaries_and_reject_invalid_or_duplicate_names(
        string collection, string _)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var boundaryName = new string('a', 80);
        var first = await client.PostAsJsonAsync(collection, new { name = $" {boundaryName} " });
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode); // Raw input length is validated before trim.

        var created = await client.PostAsJsonAsync(collection, new { name = boundaryName });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("\"1\"", created.Headers.ETag?.Tag);
        Assert.Equal(boundaryName, (await created.JsonAsync()).GetProperty("name").GetString());

        foreach (var invalid in new string?[] { null, string.Empty, "   ", new string('x', 81) })
        {
            var response = await client.PostAsJsonAsync(collection, new { name = invalid });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var duplicate = await client.PostAsJsonAsync(collection,
            new { name = boundaryName.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [MemberData(nameof(NamedCollections))]
    [Trait("Spec", "SC-006")]
    [Trait("Spec", "SC-007")]
    [Trait("Spec", "SC-008")]
    [Trait("Spec", "SC-009")]
    [Trait("Spec", "SC-013")]
    [Trait("Spec", "SC-014")]
    [Trait("Spec", "SC-015")]
    [Trait("Spec", "TE-005")]
    [Trait("Spec", "TE-006")]
    [Trait("Spec", "TE-007")]
    [Trait("Spec", "TE-013")]
    [Trait("Spec", "TE-014")]
    [Trait("Spec", "TE-015")]
    public async Task Named_resource_lifecycle_obeys_etags_soft_delete_visibility_and_restore_conflicts(
        string collection, string _)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var value = await FlowTestSupport.CreateNamedAsync(client, collection, "Original");

        using var rename = FlowTestSupport.Patch($"{collection}/{value.Id}", "{\"name\":\" Renamed \"}",
            value.ETag);
        var renamed = await client.SendAsync(rename);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Renamed", (await renamed.JsonAsync()).GetProperty("name").GetString());

        using var delete = FlowTestSupport.Delete($"{collection}/{value.Id}", renamed.Headers.ETag!.Tag);
        var deleted = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{collection}/{value.Id}")).StatusCode);

        using var includeDeleted = new HttpRequestMessage(HttpMethod.Get, collection);
        includeDeleted.Headers.Add("X-Include-Deleted", "true");
        var all = await (await client.SendAsync(includeDeleted)).JsonAsync();
        Assert.True(all.GetProperty("items")[0].GetProperty("isDeleted").GetBoolean());

        using var idempotent = FlowTestSupport.Delete($"{collection}/{value.Id}", "\"stale\"");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(idempotent)).StatusCode);

        var replacement = await client.PostAsJsonAsync(collection, new { name = "renamed" });
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
        using var conflict = FlowTestSupport.Restore($"{collection}/{value.Id}", deleted.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(conflict)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "SC-010")]
    [Trait("Spec", "SC-011")]
    [Trait("Spec", "SC-012")]
    [Trait("Spec", "SC-016")]
    [Trait("Spec", "SK-011")]
    [Trait("Spec", "SK-012")]
    [Trait("Spec", "SK-013")]
    [Trait("Spec", "SK-014")]
    public async Task Category_requires_skills_to_be_retired_and_restored_in_dependency_order()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var category = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Backend");
        var skillResponse = await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "C#", categoryId = category.Id });
        var skill = await skillResponse.JsonAsync();
        var skillId = skill.GetProperty("id").GetGuid();

        using var blockedCategory = FlowTestSupport.Delete(
            $"/api/v1/admin/skill-categories/{category.Id}", category.ETag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(blockedCategory)).StatusCode);

        using var deleteSkill = FlowTestSupport.Delete($"/api/v1/admin/skills/{skillId}",
            skillResponse.Headers.ETag!.Tag);
        var deletedSkill = await client.SendAsync(deleteSkill);
        Assert.Equal(HttpStatusCode.NoContent, deletedSkill.StatusCode);

        using var deleteCategory = FlowTestSupport.Delete(
            $"/api/v1/admin/skill-categories/{category.Id}", category.ETag);
        var deletedCategory = await client.SendAsync(deleteCategory);
        Assert.Equal(HttpStatusCode.NoContent, deletedCategory.StatusCode);

        using var restoreSkillTooSoon = FlowTestSupport.Restore($"/api/v1/admin/skills/{skillId}",
            deletedSkill.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(restoreSkillTooSoon)).StatusCode);

        using var restoreCategory = FlowTestSupport.Restore(
            $"/api/v1/admin/skill-categories/{category.Id}", deletedCategory.Headers.ETag!.Tag);
        var restoredCategory = await client.SendAsync(restoreCategory);
        Assert.Equal(HttpStatusCode.NoContent, restoredCategory.StatusCode);

        using var restoreSkill = FlowTestSupport.Restore($"/api/v1/admin/skills/{skillId}",
            deletedSkill.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(restoreSkill)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "SK-001")]
    [Trait("Spec", "SK-002")]
    [Trait("Spec", "SK-003")]
    [Trait("Spec", "SK-004")]
    [Trait("Spec", "SK-005")]
    public async Task Skill_creation_validates_category_and_scopes_name_uniqueness_per_category()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var backend = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Backend");
        var frontend = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Frontend");

        var first = await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "C#", categoryId = backend.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = " c# ", categoryId = backend.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "C#", categoryId = frontend.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "Orphan", categoryId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "Invalid", categoryId = Guid.Empty })).StatusCode);
    }

    [Fact]
    [Trait("Spec", "SK-007")]
    [Trait("Spec", "SK-008")]
    [Trait("Spec", "SK-009")]
    [Trait("Spec", "SK-010")]
    public async Task Skill_patch_moves_public_grouping_atomically_and_rejects_destination_conflict()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var backend = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Backend");
        var frontend = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Frontend");
        var skillResponse = await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "C#", categoryId = backend.Id });
        var id = (await skillResponse.JsonAsync()).GetProperty("id").GetGuid();

        using var move = FlowTestSupport.Patch($"/api/v1/admin/skills/{id}",
            $"{{\"name\":\"TypeScript\",\"categoryId\":\"{frontend.Id}\"}}",
            skillResponse.Headers.ETag!.Tag);
        var moved = await client.SendAsync(move);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(frontend.Id, (await moved.JsonAsync()).GetProperty("categoryId").GetGuid());

        await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "C#", categoryId = backend.Id });
        using var conflict = FlowTestSupport.Patch($"/api/v1/admin/skills/{id}",
            $"{{\"name\":\"C#\",\"categoryId\":\"{backend.Id}\"}}",
            moved.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(conflict)).StatusCode);
        var current = await (await client.GetAsync($"/api/v1/admin/skills/{id}")).JsonAsync();
        Assert.Equal("TypeScript", current.GetProperty("name").GetString());
        Assert.Equal(frontend.Id, current.GetProperty("categoryId").GetGuid());
    }

    [Fact]
    [Trait("Spec", "SK-015")]
    [Trait("Spec", "SK-016")]
    public async Task Skill_restore_rejects_name_conflict_missing_precondition_and_stale_precondition()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var category = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/skill-categories", "Backend");
        var original = await client.PostAsJsonAsync("/api/v1/admin/skills",
            new { name = "C#", categoryId = category.Id });
        var id = (await original.JsonAsync()).GetProperty("id").GetGuid();
        using var delete = FlowTestSupport.Delete($"/api/v1/admin/skills/{id}",
            original.Headers.ETag!.Tag);
        var deleted = await client.SendAsync(delete);
        await client.PostAsJsonAsync("/api/v1/admin/skills", new { name = " c# ", categoryId = category.Id });

        using var noMatch = FlowTestSupport.Restore($"/api/v1/admin/skills/{id}");
        Assert.Equal((HttpStatusCode)428, (await client.SendAsync(noMatch)).StatusCode);
        using var stale = FlowTestSupport.Restore($"/api/v1/admin/skills/{id}", "\"0\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var conflict = FlowTestSupport.Restore($"/api/v1/admin/skills/{id}",
            deleted.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(conflict)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "TE-008")]
    [Trait("Spec", "TE-009")]
    [Trait("Spec", "TE-010")]
    public async Task Technology_delete_is_blocked_by_each_active_parent_type()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var technology = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", ".NET");
        await client.PostAsJsonAsync("/api/v1/admin/experiences",
            FlowTestSupport.Experience(technologyIds: [technology.Id]));
        await client.PostAsJsonAsync("/api/v1/admin/projects",
            FlowTestSupport.Project(technologyIds: [technology.Id]));

        using var delete = FlowTestSupport.Delete($"/api/v1/admin/technologies/{technology.Id}",
            technology.ETag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(delete)).StatusCode);
        Assert.Equal(2, await factory.ExecuteDbAsync(async db =>
            await db.ExperienceTechnologies.CountAsync() + await db.ProjectTechnologies.CountAsync()));
    }
}
