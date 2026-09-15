using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class MutationRejectionTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    [Trait("Spec", "PCV-003")]
    [Trait("Spec", "PJ-004")]
    public async Task Image_dimensions_cannot_be_absent_or_null(int? width, int? height)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var result = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(
            image: new
            {
                url = "https://example.com/image.png",
                alt = "Preview",
                width,
                height
            }));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.True((await result.JsonAsync()).GetProperty("errors").TryGetProperty("image", out _));
    }

    [Fact]
    [Trait("Spec", "PCV-003")]
    [Trait("Spec", "PR-004")]
    public async Task Null_social_link_items_are_keyed_validation_errors()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var result = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(socialLinks: [null!]));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.True((await result.JsonAsync()).GetProperty("errors").TryGetProperty("socialLinks", out _));
    }

    [Fact]
    [Trait("Spec", "PCV-003")]
    public async Task Image_dimensions_require_JSON_numbers_as_documented()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var result = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project(
            image: new
            {
                url = "https://example.com/image.png",
                alt = "Preview",
                width = "1",
                height = "1"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(0, await factory.ExecuteDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Projects)));
        var created = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project());
        var path = $"/api/v1/admin/projects/{(await created.JsonAsync()).GetProperty("id").GetGuid()}";
        using var patch = FlowTestSupport.Patch(path,
            "{\"image\":{\"url\":\"https://example.com/image.png\",\"alt\":\"Preview\",\"width\":\"1\",\"height\":1}}", created.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(patch)).StatusCode);
        Assert.Equal(created.Headers.ETag, (await client.GetAsync(path)).Headers.ETag);
    }

    [Theory]
    [InlineData("skills")]
    [InlineData("projects")]
    public async Task Missing_targets_invalid_patch_documents_and_delete_preconditions_preserve_state(string collection)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var absent = $"/api/v1/admin/{collection}/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(absent)).StatusCode);
        using var missingPatch = FlowTestSupport.Patch(absent, "{}", "\"1\"");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingPatch)).StatusCode);
        using var missingDelete = FlowTestSupport.Delete(absent, "\"1\"");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingDelete)).StatusCode);
        using var missingRestore = FlowTestSupport.Restore(absent, "\"1\"");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingRestore)).StatusCode);
        var category = await FlowTestSupport.CreateNamedAsync(client, "/api/v1/admin/skill-categories", "Backend");
        var created = collection == "skills"
            ? await client.PostAsJsonAsync("/api/v1/admin/skills", new
            {
                name = "C#",
                categoryId = category.Id
            })
            : await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project());
        var path = $"/api/v1/admin/{collection}/{(await created.JsonAsync()).GetProperty("id").GetGuid()}";
        var etag = created.Headers.ETag!.ToString();
        var before = await created.Content.ReadAsStringAsync();
        using var invalidDocument = FlowTestSupport.Patch(path, "[]", etag);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalidDocument)).StatusCode);
        using var invalidRequired = FlowTestSupport.Patch(path, "{\"name\":null}", etag);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalidRequired)).StatusCode);
        using var invalidParent = FlowTestSupport.Patch(path,
            collection == "skills" ? $"{{\"categoryId\":\"{Guid.NewGuid()}\"}}" : $"{{\"technologyIds\":[\"{Guid.NewGuid()}\"]}}", etag);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalidParent)).StatusCode);
        using var noMatch = FlowTestSupport.Delete(path);
        Assert.Equal((HttpStatusCode)428, (await client.SendAsync(noMatch)).StatusCode);
        using var stale = FlowTestSupport.Delete(path, "\"0\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var activeRestore = FlowTestSupport.Restore(path, etag);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(activeRestore)).StatusCode);
        Assert.Equal(before, await (await client.GetAsync(path)).Content.ReadAsStringAsync());
        using var delete = FlowTestSupport.Delete(path, etag);
        var deleted = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var repeatDelete = FlowTestSupport.Delete(path, etag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(repeatDelete)).StatusCode);
        using var staleRestore = FlowTestSupport.Restore(path, etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(staleRestore)).StatusCode);
        using var restore = FlowTestSupport.Restore(path, deleted.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(restore)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }
}
