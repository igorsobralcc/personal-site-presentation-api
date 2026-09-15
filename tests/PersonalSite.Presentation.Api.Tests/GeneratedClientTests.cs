using System.Net;
using System.Text.Json;
using PersonalSite.Presentation.Client;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class GeneratedClientTests
{
    [Fact]
    [Trait("Spec", "PCV-005")]
    public async Task Generated_public_and_admin_client_handles_real_errors_creation_patch_and_revalidation()
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateApiClient();
        var client = new PresentationClient(http);
        using var missing = await client.GetPresentationAsync();
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var request = JsonSerializer.SerializeToElement(FlowTestSupport.Profile());
        using var created = await client.InitializeProfileAsync(request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var read = await client.GetAdminProfileAsync();
        Assert.Equal("Igor", (await read.JsonAsync()).GetProperty("fullName").GetString());
        using var patch = await client.PatchProfileAsync(JsonSerializer.SerializeToElement(new
        {
            headline = "Architect"
        }),
            ifMatch: read.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        using var publicRead = await client.GetPresentationAsync();
        Assert.Equal("Architect", (await publicRead.JsonAsync()).GetProperty("profile").GetProperty("headline").GetString());
        using var cached = await client.GetPresentationAsync(ifNoneMatch: publicRead.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotModified, cached.StatusCode);
        Assert.Empty(await cached.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("{\"fullName\":5}", "{\"type\":\"object\",\"properties\":{\"fullName\":{\"type\":\"string\"}}}")]
    [InlineData("{}", "{\"type\":\"object\",\"required\":[\"id\"]}")]
    [InlineData("[\"same\",\"same\"]", "{\"type\":\"array\",\"uniqueItems\":true}")]
    [Trait("Spec", "PCV-003")]
    [Trait("Spec", "PQG-006")]
    public void Deliberate_runtime_payload_schema_mismatches_fail(string payload, string contract)
    {
        using var value = JsonDocument.Parse(payload);
        using var schema = JsonDocument.Parse(contract);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => JsonSchemaAssertions.Validate(value.RootElement, schema.RootElement, schema.RootElement));
    }
}
