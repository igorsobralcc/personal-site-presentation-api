using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class PlatformFlowTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    [Trait("Spec", "PF-002")]
    [Trait("Spec", "PF-028")]
    public async Task Admin_gate_rejects_every_invalid_key_without_touching_the_handler(string? supplied)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        if (supplied is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Admin-Key", supplied);
        }

        var response = await client.GetAsync("/api/v1/admin/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.JsonAsync()).GetProperty("traceId").GetString()?.Length > 0);
    }

    [Fact]
    [Trait("Spec", "PF-002")]
    public async Task Admin_gate_is_closed_when_no_key_is_configured()
    {
        await using var factory = new ApiFactory(adminKey: null);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/admin/profile")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PF-003")]
    [Trait("Spec", "PF-004")]
    public async Task Admin_transport_gate_allows_development_http_but_rejects_production_http()
    {
        await using var development = new ApiFactory();
        using var developmentClient = development.CreateClient(new()
        {
            BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
        });
        developmentClient.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        Assert.Equal(HttpStatusCode.NotFound,
            (await developmentClient.GetAsync("/api/v1/admin/profile")).StatusCode);

        await using var production = new ApiFactory(environment: "Production");
        using var productionClient = production.CreateClient(new()
        {
            BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
        });
        productionClient.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await productionClient.GetAsync("/api/v1/admin/profile")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PF-005")]
    [Trait("Spec", "PF-006")]
    [Trait("Spec", "PF-008")]
    public async Task Collections_apply_defaults_boundaries_and_beyond_last_page()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await client.PostAsJsonAsync("/api/v1/admin/technologies", new { name = "One" });

        var defaults = await (await client.GetAsync("/api/v1/admin/technologies")).JsonAsync();
        Assert.Equal(1, defaults.GetProperty("page").GetInt32());
        Assert.Equal(20, defaults.GetProperty("pageSize").GetInt32());

        using var boundary = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/technologies");
        boundary.Headers.Add("X-Page", int.MaxValue.ToString());
        boundary.Headers.Add("X-Page-Size", "1");
        boundary.Headers.Add("X-Include-Deleted", "false");
        var page = await client.SendAsync(boundary);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var body = await page.JsonAsync();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(int.MaxValue, body.GetProperty("page").GetInt32());
        Assert.Equal(1, body.GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("X-Page", "0")]
    [InlineData("X-Page", "abc")]
    [InlineData("X-Page-Size", "0")]
    [InlineData("X-Page-Size", "101")]
    [InlineData("X-Include-Deleted", "sometimes")]
    [Trait("Spec", "PF-007")]
    public async Task Collections_reject_invalid_pagination_headers(string header, string value)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/technologies");
        request.Headers.TryAddWithoutValidation(header, value);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.JsonAsync()).GetProperty("errors").TryGetProperty(header, out _));
    }

    [Fact]
    [Trait("Spec", "PF-007")]
    public async Task Collections_return_all_invalid_pagination_headers_together()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/technologies");
        request.Headers.TryAddWithoutValidation("X-Page", "0");
        request.Headers.TryAddWithoutValidation("X-Page-Size", "101");
        request.Headers.TryAddWithoutValidation("X-Include-Deleted", "nope");
        var errors = (await (await client.SendAsync(request)).JsonAsync()).GetProperty("errors");
        Assert.Equal(3, errors.EnumerateObject().Count());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [Trait("Spec", "PF-011")]
    public async Task Merge_patch_rejects_non_object_documents(string json)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", "PostgreSQL");
        using var request = FlowTestSupport.Patch($"/api/v1/admin/technologies/{created.Id}", json,
            created.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PF-031")]
    public async Task Merge_patch_rejects_an_unsupported_media_type()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await FlowTestSupport.CreateNamedAsync(client,
            "/api/v1/admin/technologies", "PostgreSQL");
        using var request = FlowTestSupport.Patch($"/api/v1/admin/technologies/{created.Id}",
            "{\"name\":\"SQL\"}", created.ETag, "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PF-027")]
    public async Task Invalid_route_and_method_never_reach_a_feature_handler()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/admin/technologies/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.PutAsJsonAsync("/api/v1/admin/technologies", new { name = "No" })).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PF-023")]
    [Trait("Spec", "PF-024")]
    public async Task Cors_grants_only_the_configured_origin()
    {
        await using var factory = new ApiFactory(configureTestServices: services =>
        {
            services.AddCors(options => options.AddDefaultPolicy(policy =>
                policy.WithOrigins("https://allowed.example").AllowAnyHeader().AllowAnyMethod()));
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/v1/presentation");
        allowed.Headers.Add("Origin", "https://allowed.example");
        allowed.Headers.Add("Access-Control-Request-Method", "GET");
        var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal("https://allowed.example",
            allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/v1/presentation");
        denied.Headers.Add("Origin", "https://denied.example");
        denied.Headers.Add("Access-Control-Request-Method", "GET");
        var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
