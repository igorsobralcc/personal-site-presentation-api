using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class AcceptanceTests
{
    [Fact]
    public async Task Management_requires_admin_key_and_returns_problem_details()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        var response = await client.GetAsync("/api/v1/admin/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Profile_is_singleton_and_merge_patch_preserves_omitted_fields()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var profile = new
        {
            fullName = "Igor",
            headline = "Engineer",
            biography = "Builds things",
            shortSummary = (string?)null,
            location = "Brazil",
            email = "igor@example.com",
            availability = (string?)null,
            currentFocus = "APIs",
            socialLinks = new[] { new { label = "GitHub", url = "https://github.com/igor" } }
        };
        var created = await client.PutAsJsonAsync("/api/v1/admin/profile", profile);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var etag = created.Headers.ETag!.Tag;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/v1/admin/profile", profile)).StatusCode);
        using var patch = Patch("{\"currentFocus\":\"Distributed systems\"}");
        patch.Headers.TryAddWithoutValidation("If-Match", etag);
        var updated = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Igor", body.GetProperty("fullName").GetString());
        Assert.Equal("Distributed systems", body.GetProperty("currentFocus").GetString());
        Assert.Single(body.GetProperty("socialLinks").EnumerateArray());
        using var replace = Patch("{\"socialLinks\":[{\"label\":\"LinkedIn\",\"url\":\"https://linkedin.com/in/igor\"}]}");
        replace.Headers.TryAddWithoutValidation("If-Match", updated.Headers.ETag!.Tag);
        var replaced = await client.SendAsync(replace);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var replacedBody = await replaced.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(replacedBody.GetProperty("socialLinks").EnumerateArray());
        Assert.Equal("LinkedIn", replacedBody.GetProperty("socialLinks")[0].GetProperty("label").GetString());
    }

    [Fact]
    public async Task Stale_etag_cannot_overwrite_a_resource()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await client.PostAsJsonAsync("/api/v1/admin/technologies", new
        {
            name = ".NET"
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var old = created.Headers.ETag!.Tag;
        using var first = Patch("{\"name\":\".NET 10\"}", $"/api/v1/admin/technologies/{id}");
        first.Headers.TryAddWithoutValidation("If-Match", old);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(first)).StatusCode);
        using var stale = Patch("{\"name\":\"Legacy\"}", $"/api/v1/admin/technologies/{id}");
        stale.Headers.TryAddWithoutValidation("If-Match", old);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
    }

    [Fact]
    public async Task Public_projection_is_ordered_filtered_and_cacheable()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await InitializeProfile(client);
        var category = await client.PostAsJsonAsync("/api/v1/admin/skill-categories", new
        {
            name = "Backend"
        });
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostAsJsonAsync("/api/v1/admin/skills", new
        {
            name = "C#",
            categoryId
        });
        await client.PostAsJsonAsync("/api/v1/admin/experiences", new
        {
            company = "Older",
            role = "Dev",
            location = (string?)null,
            startDate = "2020-01-01",
            endDate = "2021-01-01",
            summary = "Old",
            highlights = Array.Empty<string>(),
            technologyIds = Array.Empty<Guid>()
        });
        await client.PostAsJsonAsync("/api/v1/admin/experiences", new
        {
            company = "Newer",
            role = "Lead",
            location = (string?)null,
            startDate = "2024-01-01",
            endDate = (string?)null,
            summary = "New",
            highlights = Array.Empty<string>(),
            technologyIds = Array.Empty<Guid>()
        });
        await client.PostAsJsonAsync("/api/v1/admin/projects", new
        {
            name = "Hidden",
            summary = "Not featured",
            repositoryUrl = (string?)null,
            liveUrl = (string?)null,
            technologyIds = Array.Empty<Guid>(),
            isFeatured = false,
            image = (object?)null
        });
        await client.PostAsJsonAsync("/api/v1/admin/projects", new
        {
            name = "Visible",
            summary = "Featured",
            repositoryUrl = "https://example.com/repo",
            liveUrl = (string?)null,
            technologyIds = Array.Empty<Guid>(),
            isFeatured = true,
            image = (object?)null
        });
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var response = await client.GetAsync("/api/v1/presentation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.Public);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Newer", body.GetProperty("experiences")[0].GetProperty("company").GetString());
        Assert.Single(body.GetProperty("projects").EnumerateArray());
        Assert.Equal("Visible", body.GetProperty("projects")[0].GetProperty("name").GetString());
        Assert.Equal("C#", body.GetProperty("skillCategories")[0].GetProperty("skills")[0].GetProperty("name").GetString());
        using var cached = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        cached.Headers.TryAddWithoutValidation("If-None-Match", response.Headers.ETag.Tag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(cached)).StatusCode);
    }

    [Fact]
    public async Task Collections_page_and_soft_deleted_items_are_hidden_by_default()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PostAsJsonAsync("/api/v1/admin/technologies", new
        {
            name = "One"
        });
        var second = await client.PostAsJsonAsync("/api/v1/admin/technologies", new
        {
            name = "Two"
        });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/technologies/{secondBody.GetProperty("id").GetGuid()}");
        delete.Headers.TryAddWithoutValidation("If-Match", second.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/technologies");
        list.Headers.Add("X-Page", "1");
        list.Headers.Add("X-Page-Size", "1");
        var page = await client.SendAsync(list);
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("totalItems").GetInt32());
        Assert.Single(body.GetProperty("items").EnumerateArray());
        using var all = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/technologies");
        all.Headers.Add("X-Include-Deleted", "true");
        var allBody = await (await client.SendAsync(all)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, allBody.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Referenced_technology_is_protected_and_names_are_case_insensitively_unique()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var technology = await client.PostAsJsonAsync("/api/v1/admin/technologies", new
        {
            name = ".NET"
        });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/admin/technologies", new
        {
            name = ".net"
        })).StatusCode);
        var technologyId = (await technology.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/admin/projects", new
        {
            name = "API",
            summary = "Service",
            repositoryUrl = (string?)null,
            liveUrl = (string?)null,
            technologyIds = new[] { technologyId },
            isFeatured = true,
            image = (object?)null
        })).StatusCode);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/technologies/{technologyId}");
        delete.Headers.TryAddWithoutValidation("If-Match", technology.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(delete)).StatusCode);
    }

    [Fact]
    public async Task Delete_requires_precondition_and_is_idempotent_after_deletion()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await client.PostAsJsonAsync("/api/v1/admin/technologies", new
        {
            name = "PostgreSQL"
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal((HttpStatusCode)428, (await client.DeleteAsync($"/api/v1/admin/technologies/{id}")).StatusCode);
        using var first = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/technologies/{id}");
        first.Headers.TryAddWithoutValidation("If-Match", created.Headers.ETag!.Tag);
        var deleted = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var again = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/technologies/{id}");
        again.Headers.TryAddWithoutValidation("If-Match", "\"stale\"");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(again)).StatusCode);
    }

    [Fact]
    public async Task Hidden_experience_changes_do_not_invalidate_public_etag()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await InitializeProfile(client);
        var created = await client.PostAsJsonAsync("/api/v1/admin/experiences", new
        {
            company = "Company",
            role = "Engineer",
            location = "Private office",
            startDate = "2024-01-01",
            endDate = (string?)null,
            summary = "Public",
            highlights = new[] { "Hidden one" },
            technologyIds = Array.Empty<Guid>()
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        var before = await client.GetAsync("/api/v1/presentation");
        var publicBody = await before.Content.ReadFromJsonAsync<JsonElement>();
        var experience = publicBody.GetProperty("experiences")[0];
        Assert.False(experience.TryGetProperty("location", out _));
        Assert.False(experience.TryGetProperty("highlights", out _));
        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        var current = await client.GetAsync($"/api/v1/admin/experiences/{id}");
        Assert.Equal(created.Headers.ETag!.Tag, current.Headers.ETag!.Tag);
        using var patch = Patch("{\"highlights\":[\"Hidden two\"]}", $"/api/v1/admin/experiences/{id}");
        patch.Headers.TryAddWithoutValidation("If-Match", current.Headers.ETag.Tag);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(patch)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        using var cached = new HttpRequestMessage(HttpMethod.Get, "/api/v1/presentation");
        cached.Headers.TryAddWithoutValidation("If-None-Match", before.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(cached)).StatusCode);
    }

    [Fact]
    public async Task Aggregate_validation_rejects_invalid_dates_and_incomplete_media()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var experience = await client.PostAsJsonAsync("/api/v1/admin/experiences", new
        {
            company = "Company",
            role = "Role",
            location = (string?)null,
            startDate = "2024-01-01",
            endDate = "2023-01-01",
            summary = "Summary",
            highlights = Array.Empty<string>(),
            technologyIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, experience.StatusCode);
        var project = await client.PostAsJsonAsync("/api/v1/admin/projects", new
        {
            name = "Project",
            summary = "Summary",
            repositoryUrl = (string?)null,
            liveUrl = (string?)null,
            technologyIds = Array.Empty<Guid>(),
            isFeatured = true,
            image = new
            {
                url = "https://example.com/image.png",
                alt = "Image",
                width = 0,
                height = 100
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, project.StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_are_anonymous_and_report_named_readiness()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = await ready.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("presentation_database", body.GetProperty("checks")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Database_outage_fails_readiness_but_not_liveness()
    {
        await using var factory = new ApiFactory(failingReadiness: true);
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    private static HttpRequestMessage Patch(string json, string uri = "/api/v1/admin/profile")
    {
        return new(HttpMethod.Patch, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/merge-patch+json")
        };
    }

    private static Task<HttpResponseMessage> InitializeProfile(HttpClient client)
    {
        return client.PutAsJsonAsync("/api/v1/admin/profile", new
        {
            fullName = "Igor",
            headline = "Engineer",
            biography = "Bio",
            shortSummary = (string?)null,
            location = (string?)null,
            email = (string?)null,
            availability = (string?)null,
            currentFocus = (string?)null,
            socialLinks = Array.Empty<object>()
        });
    }
}
