using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PersonalSite.Presentation.Api.Tests;

internal static class FlowTestSupport
{
    public static object Profile(
        string? fullName = "Igor",
        string? headline = "Engineer",
        string? biography = "Builds reliable systems",
        string? shortSummary = null,
        string? location = null,
        string? email = null,
        string? availability = null,
        string? currentFocus = null,
        object[]? socialLinks = null) => new
        {
            fullName,
            headline,
            biography,
            shortSummary,
            location,
            email,
            availability,
            currentFocus,
            socialLinks = socialLinks ?? []
        };

    public static object Experience(
        string? company = "Company",
        string? role = "Engineer",
        string? location = null,
        string? startDate = "2024-01-01",
        string? endDate = null,
        string? summary = "Summary",
        string[]? highlights = null,
        Guid[]? technologyIds = null) => new
        {
            company,
            role,
            location,
            startDate,
            endDate,
            summary,
            highlights = highlights ?? [],
            technologyIds = technologyIds ?? []
        };

    public static object Project(
        string? name = "Project",
        string? summary = "Summary",
        string? repositoryUrl = null,
        string? liveUrl = null,
        Guid[]? technologyIds = null,
        bool? isFeatured = true,
        object? image = null) => new
        {
            name,
            summary,
            repositoryUrl,
            liveUrl,
            technologyIds = technologyIds ?? [],
            isFeatured,
            image
        };

    public static HttpRequestMessage Patch(string uri, string json, string? etag = null,
        string mediaType = "application/merge-patch+json")
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    public static HttpRequestMessage Delete(string uri, string? etag = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    public static HttpRequestMessage Restore(string uri, string? etag = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{uri.TrimEnd('/')}/restore");
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    public static async Task<(Guid Id, string ETag, JsonElement Body)> CreateNamedAsync(
        HttpClient client, string collection, string name)
    {
        var response = await client.PostAsJsonAsync(collection, new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), response.Headers.ETag!.Tag, body);
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
