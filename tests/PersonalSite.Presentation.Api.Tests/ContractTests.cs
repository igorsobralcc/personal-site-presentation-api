using System.Net;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using PersonalSite.Presentation.Api.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class ContractTests
{
    [Fact]
    [Trait("Spec", "PCV-003")]
    public async Task Generated_contract_preserves_all_operations_schemas_statuses_parameters_media_and_headers()
    {
        var logs = new TestLoggerProvider();
        await using var factory = new ApiFactory(configureTestServices: services =>
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(logs));
        using var client = factory.CreateApiClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            string.Join("; ", logs.Entries.Where(x => x.Exception is not null).Select(x => x.Exception!.Message)));
        var generated = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "generated-openapi.json"), generated.ToJsonString());
        var reviewed = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "openapi.json")))!;
        Assert.True(JsonNode.DeepEquals(reviewed["paths"], generated["paths"]));
        Assert.True(JsonNode.DeepEquals(reviewed["components"], generated["components"]));
    }

    [Fact]
    [Trait("Spec", "PCV-003")]
    [Trait("Spec", "PQG-006")]
    public async Task Runtime_operation_and_binding_drift_fails_document_generation()
    {
        var text = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "openapi.json"));
        var document = OpenApiDocument.Parse(text, "json").Document!;
        document.Paths.Remove("/api/v1/presentation");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PresentationOpenApiTransformer()
            .TransformAsync(document, null!, default));
        document = OpenApiDocument.Parse(text, "json").Document!;
        document.Paths["/api/v1/admin/profile"].Operations![HttpMethod.Put].RequestBody!.Content!.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PresentationOpenApiTransformer()
            .TransformAsync(document, null!, default));
        document = OpenApiDocument.Parse(text, "json").Document!;
        document.Paths["/api/v1/admin/profile"].Operations![HttpMethod.Put].RequestBody!.Content!["application/json"].Schema =
            new OpenApiSchema { Type = JsonSchemaType.Object, Properties = new Dictionary<string, IOpenApiSchema>() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PresentationOpenApiTransformer()
            .TransformAsync(document, null!, default));
    }
}
