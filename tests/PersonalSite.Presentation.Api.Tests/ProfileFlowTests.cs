using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class ProfileFlowTests
{
    [Fact]
    [Trait("Spec", "PR-001")]
    [Trait("Spec", "PR-002")]
    [Trait("Spec", "PR-009")]
    [Trait("Spec", "PF-001")]
    public async Task Initialization_trims_and_round_trips_the_complete_ordered_aggregate()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var links = Enumerable.Range(1, 20)
            .Select(index => (object)new { label = $" Link {index} ", url = $"https://example.com/{index}" })
            .ToArray();
        var response = await client.PutAsJsonAsync("/api/v1/admin/profile",
            FlowTestSupport.Profile(" Igor ", " Engineer ", " Biography ", " Summary ", " Brazil ",
                "igor@example.com", " Available ", " APIs ", links));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/admin/profile", response.Headers.Location?.OriginalString);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        var body = await response.JsonAsync();
        Assert.Equal("Igor", body.GetProperty("fullName").GetString());
        Assert.Equal("Link 1", body.GetProperty("socialLinks")[0].GetProperty("label").GetString());
        Assert.Equal(20, body.GetProperty("socialLinks").GetArrayLength());

        var read = await client.GetAsync("/api/v1/admin/profile");
        Assert.Equal(response.Headers.ETag?.Tag, read.Headers.ETag?.Tag);
    }

    [Theory]
    [InlineData("fullName", null)]
    [InlineData("fullName", "")]
    [InlineData("fullName", "   ")]
    [InlineData("headline", null)]
    [InlineData("biography", null)]
    [Trait("Spec", "PR-003")]
    public async Task Initialization_rejects_missing_required_text(string field, string? value)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var payload = JsonSerializer.SerializeToNode(FlowTestSupport.Profile())!.AsObject();
        payload[field] = value;
        var response = await client.PutAsJsonAsync("/api/v1/admin/profile", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.JsonAsync()).GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.Profiles.CountAsync()));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [Trait("Spec", "PR-004")]
    public async Task Initialization_rejects_invalid_email(string email)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var response = await client.PutAsJsonAsync("/api/v1/admin/profile",
            FlowTestSupport.Profile(email: email));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PR-005")]
    public async Task Initialization_rejects_links_that_duplicate_after_trim_and_case_normalization()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var response = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            socialLinks:
            [
                new { label = "GitHub", url = "https://github.com/one" },
                new { label = " github ", url = "https://github.com/two" }
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.ProfileSocialLinks.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PR-007")]
    public async Task Second_initialization_preserves_the_original_profile()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var first = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile());
        var second = await client.PutAsJsonAsync("/api/v1/admin/profile",
            FlowTestSupport.Profile(fullName: "Replacement"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Igor", (await (await client.GetAsync("/api/v1/admin/profile")).JsonAsync())
            .GetProperty("fullName").GetString());
        Assert.Equal("\"1\"", first.Headers.ETag?.Tag);
    }

    [Fact]
    [Trait("Spec", "PR-010")]
    public async Task Missing_profile_wins_over_missing_patch_precondition()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/admin/profile")).StatusCode);
        using var patch = FlowTestSupport.Patch("/api/v1/admin/profile", "{\"currentFocus\":\"APIs\"}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(patch)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PR-011")]
    [Trait("Spec", "PR-012")]
    [Trait("Spec", "PR-013")]
    [Trait("Spec", "PF-010")]
    [Trait("Spec", "PF-015")]
    public async Task Patch_preserves_omissions_clears_nullable_values_and_replaces_links()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            location: "Brazil", currentFocus: "Old",
            socialLinks: [new { label = "GitHub", url = "https://github.com/igor" }]));
        using var patch = FlowTestSupport.Patch("/api/v1/admin/profile",
            "{\"location\":null,\"currentFocus\":\"New\",\"socialLinks\":[]}",
            created.Headers.ETag!.Tag);
        var response = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
        var body = await response.JsonAsync();
        Assert.Equal("Igor", body.GetProperty("fullName").GetString());
        Assert.False(body.TryGetProperty("location", out _));
        Assert.Empty(body.GetProperty("socialLinks").EnumerateArray());
        Assert.Equal(0, await factory.ExecuteDbAsync(db => db.ProfileSocialLinks.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PR-014")]
    [Trait("Spec", "PR-015")]
    [Trait("Spec", "PF-013")]
    [Trait("Spec", "PF-014")]
    public async Task Invalid_link_replacement_and_preconditions_leave_profile_unchanged()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await client.PutAsJsonAsync("/api/v1/admin/profile", FlowTestSupport.Profile(
            socialLinks: [new { label = "GitHub", url = "https://github.com/igor" }]));
        using var invalid = FlowTestSupport.Patch("/api/v1/admin/profile", "{\"socialLinks\":null}",
            created.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalid)).StatusCode);
        using var missing = FlowTestSupport.Patch("/api/v1/admin/profile", "{\"currentFocus\":\"No\"}");
        Assert.Equal((HttpStatusCode)428, (await client.SendAsync(missing)).StatusCode);
        using var stale = FlowTestSupport.Patch("/api/v1/admin/profile", "{\"currentFocus\":\"No\"}",
            "\"0\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        var body = await (await client.GetAsync("/api/v1/admin/profile")).JsonAsync();
        Assert.Single(body.GetProperty("socialLinks").EnumerateArray());
        Assert.Equal(1, body.GetProperty("version").GetInt64());
    }
}
