using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class CacheEdgeTests
{
    [Theory]
    [InlineData("*", true)]
    [InlineData("W/{etag}", true)]
    [InlineData("\"other\", {etag}", true)]
    [InlineData("W/\"other\", W/{etag}", true)]
    [InlineData("\"other\"", false)]
    [InlineData("invalid", false)]
    [InlineData("\"unterminated", false)]
    [Trait("Spec", "PPC-001")]
    [Trait("Spec", "PP-016")]
    public async Task Conditional_get_uses_weak_list_and_wildcard_comparison(string value, bool matches)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var current = await client.GetAsync("/api/v1/presentation");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        request.Headers.TryAddWithoutValidation("If-None-Match", value.Replace("{etag}", current.Headers.ETag!.ToString(), StringComparison.Ordinal));
        var response = await client.SendAsync(request);
        Assert.Equal(matches ? HttpStatusCode.NotModified : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(current.Headers.ETag, response.Headers.ETag);
        Assert.Equal(current.Headers.CacheControl, response.Headers.CacheControl);
        if (matches)
        {
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    [Trait("Spec", "PPC-001")]
    [Trait("Spec", "PP-016")]
    public async Task Separate_validator_values_match_and_wildcard_does_not_hide_missing_profile()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-None-Match", "*");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/presentation")).StatusCode);
        client.DefaultRequestHeaders.Remove("If-None-Match");
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var current = await client.GetAsync("/api/v1/presentation");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        request.Headers.TryAddWithoutValidation("If-None-Match", new[] { "\"other\"", current.Headers.ETag!.ToString() });
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Spec", "PPC-002")]
    [Trait("Spec", "PPC-003")]
    [Trait("Spec", "PP-017")]
    public async Task Removing_newest_visible_project_can_decrease_updatedAt_and_changes_ETag(bool delete)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var result = await client.PostAsJsonAsync("/api/v1/admin/projects", FlowTestSupport.Project());
        var id = (await result.JsonAsync()).GetProperty("id").GetGuid();
        await factory.ExecuteDbAsync(async db =>
        {
            var profile = await db.Profiles.SingleAsync();
            profile.PublicUpdatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            var project = await db.Projects.SingleAsync();
            project.PublicUpdatedAt = profile.PublicUpdatedAt.AddDays(1);
            await db.SaveChangesAsync();
        });
        var before = await client.GetAsync("/api/v1/presentation");
        var admin = await client.GetAsync($"/api/v1/admin/projects/{id}");
        using var mutation = delete
            ? FlowTestSupport.Delete($"/api/v1/admin/projects/{id}", admin.Headers.ETag!.ToString())
            : FlowTestSupport.Patch($"/api/v1/admin/projects/{id}", "{\"isFeatured\":false}", admin.Headers.ETag!.ToString());
        Assert.True((await client.SendAsync(mutation)).IsSuccessStatusCode);
        var after = await client.GetAsync("/api/v1/presentation");
        Assert.NotEqual(before.Headers.ETag, after.Headers.ETag);
        Assert.True((await after.JsonAsync()).GetProperty("updatedAt").GetDateTimeOffset()
            < (await before.JsonAsync()).GetProperty("updatedAt").GetDateTimeOffset());
    }
}
