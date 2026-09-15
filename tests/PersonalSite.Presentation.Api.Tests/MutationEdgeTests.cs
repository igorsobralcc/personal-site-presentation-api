using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class MutationEdgeTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"unknown\":{\"name\":\"ignored\"}}")]
    [Trait("Spec", "PMV-001")]
    [Trait("Spec", "PF-012")]
    [Trait("Spec", "PR-018")]
    [Trait("Spec", "SC-017")]
    [Trait("Spec", "TE-018")]
    [Trait("Spec", "SK-018")]
    [Trait("Spec", "EX-024")]
    [Trait("Spec", "PJ-023")]
    public async Task No_op_patches_preserve_representations_children_timestamps_and_validators(string json)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            socialLinks: [new { label = "GitHub", url = "https://github.com/igor" }]));
        var category = await FlowTestSupport.CreateNamedAsync(client, "/api/v1/admin/skill-categories", "Backend");
        var technology = await FlowTestSupport.CreateNamedAsync(client, "/api/v1/admin/technologies", "C#");
        var skill = await client.PostAsJsonAsync("/api/v1/admin/skills", new
        {
            name = "C#",
            categoryId = category.Id
        });
        var experience = await client.PostAsJsonAsync("/api/v1/admin/experiences", FlowTestSupport.Experience(
            highlights: ["Shipped"], technologyIds: [technology.Id]));
        var project = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(technologyIds: [technology.Id]));
        var paths = new[]
        {
            "/api/v1/admin/profile", $"/api/v1/admin/skill-categories/{category.Id}",
            $"/api/v1/admin/technologies/{technology.Id}",
            $"/api/v1/admin/skills/{(await skill.JsonAsync()).GetProperty("id").GetGuid()}",
            $"/api/v1/admin/experiences/{(await experience.JsonAsync()).GetProperty("id").GetGuid()}",
            $"/api/v1/admin/projects/{(await project.JsonAsync()).GetProperty("id").GetGuid()}"
        };
        var publicBefore = await client.GetAsync("/api/v1/presentation");
        var childIds = await factory.ExecuteDbAsync(async db => new[]
        {
            await db.ProfileSocialLinks.Select(x => x.Id).SingleAsync(),
            await db.ExperienceHighlights.Select(x => x.Id).SingleAsync()
        });
        foreach (var path in paths)
        {
            var before = await client.GetAsync(path);
            var body = await before.Content.ReadAsStringAsync();
            using var patch = FlowTestSupport.Patch(path, json, before.Headers.ETag!.ToString());
            var result = await client.SendAsync(patch);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(before.Headers.ETag, result.Headers.ETag);
            Assert.Equal(body, await result.Content.ReadAsStringAsync());
            Assert.Equal(body, await (await client.GetAsync(path)).Content.ReadAsStringAsync());
            using var stale = FlowTestSupport.Patch(path, json, "\"0\"");
            Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
            using var missing = FlowTestSupport.Patch(path, json);
            Assert.Equal((HttpStatusCode)428, (await client.SendAsync(missing)).StatusCode);
        }
        Assert.Equal(childIds, await factory.ExecuteDbAsync(async db => new[]
        {
            await db.ProfileSocialLinks.Select(x => x.Id).SingleAsync(),
            await db.ExperienceHighlights.Select(x => x.Id).SingleAsync()
        }));
        var publicAfter = await client.GetAsync("/api/v1/presentation");
        Assert.Equal(publicBefore.Headers.ETag, publicAfter.Headers.ETag);
        Assert.Equal(await publicBefore.Content.ReadAsStringAsync(), await publicAfter.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(2049, false)]
    [Trait("Spec", "PMV-002")]
    [Trait("Spec", "PR-006")]
    [Trait("Spec", "PJ-005")]
    public async Task Url_storage_boundaries_are_validated_before_writing(int length, bool accepted)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var url = "https://example.com/" + new string('x', length - "https://example.com/".Length);
        var profile = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            socialLinks: [new { label = "Site", url }]));
        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, profile.StatusCode);
        foreach (var request in new[]
        {
            FlowTestSupport.Project(repositoryUrl: url), FlowTestSupport.Project(liveUrl: url),
            FlowTestSupport.Project(image: new { url, alt = "Preview", width = 1, height = 1 })
        })
        {
            var result = await client.PostAsJsonAsync("/api/v1/admin/projects", request);
            Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, result.StatusCode);
            if (!accepted)
            {
                Assert.True((await result.JsonAsync()).GetProperty("errors").EnumerateObject().Any());
            }
        }
        if (!accepted)
        {
            Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Projects.CountAsync()));
            Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Profiles.CountAsync()));
        }
    }

    [Theory]
    [InlineData("Shipped", " shipped ", false)]
    [InlineData(" Shipped ", "SHIPPED", false)]
    [InlineData("Shipped", "Delivered", true)]
    [Trait("Spec", "PMV-003")]
    [Trait("Spec", "EX-007")]
    public async Task Highlights_use_trimmed_case_insensitive_uniqueness(string first, string second, bool accepted)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var result = await client.PostAsJsonAsync("/api/v1/admin/experiences", FlowTestSupport.Experience(highlights: [first, second]));
        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(accepted ? 1 : 0, await factory.ExecuteDbAsync(db => db.Experiences.CountAsync()));
        if (!accepted)
        {
            Assert.True((await result.JsonAsync()).GetProperty("errors").TryGetProperty("highlights", out _));
        }
    }

    [Theory]
    [InlineData(2147483647, 1, true)]
    [InlineData(1073741824, 2, true)]
    [InlineData(1073741825, 2, false)]
    [InlineData(2147483647, 100, false)]
    [Trait("Spec", "PMV-005")]
    [Trait("Spec", "PF-009")]
    public async Task Pagination_rejects_only_offsets_that_exceed_Int32(int page, int pageSize, bool accepted)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        foreach (var collection in new[] { "technologies", "skill-categories", "skills", "experiences", "projects" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/{collection}");
            request.Headers.Add("X-Page", page.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.Add("X-Page-Size", pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var response = await client.SendAsync(request);
            Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.JsonAsync();
            if (accepted)
            {
                Assert.Empty(body.GetProperty("items").EnumerateArray());
            }
            else
            {
                Assert.True(body.GetProperty("errors").TryGetProperty("X-Page", out _));
            }
        }
    }
}
