using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PersonalSite.Presentation.Api.Features;

/// <summary>Supplies reviewed filter, header and merge-patch metadata after verifying runtime operations.</summary>
public sealed class PresentationOpenApiTransformer : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        await using var stream = typeof(PresentationOpenApiTransformer).Assembly
            .GetManifestResourceStream("Presentation.OpenApi.json")
            ?? throw new InvalidOperationException("The reviewed OpenAPI contract is missing.");
        var result = await OpenApiDocument.LoadAsync(stream, "json", cancellationToken: cancellationToken);
        var reviewed = result.Document ?? throw new InvalidOperationException("The reviewed OpenAPI contract is invalid.");
        if (result.Diagnostic?.Errors.Count > 0)
        {
            throw new InvalidOperationException("The reviewed OpenAPI contract contains parsing errors.");
        }

        var runtimeOperations = document.Paths.SelectMany(path => path.Value.Operations!.Keys
            .Select(method => $"{method} {path.Key}")).Order().ToArray();
        var reviewedOperations = reviewed.Paths.SelectMany(path => path.Value.Operations!.Keys
            .Select(method => $"{method} {path.Key}")).Order().ToArray();
        if (!runtimeOperations.SequenceEqual(reviewedOperations))
        {
            throw new InvalidOperationException("Runtime methods and paths differ from the reviewed OpenAPI contract.");
        }

        foreach (var path in document.Paths)
        {
            foreach (var operation in path.Value.Operations!)
            {
                var expected = reviewed.Paths[path.Key].Operations![operation.Key];
                var actualMedia = operation.Value.RequestBody?.Content?.Keys.Order().ToArray() ?? [];
                var expectedMedia = expected.RequestBody?.Content?.Keys.Order().ToArray() ?? [];
                if (!actualMedia.SequenceEqual(expectedMedia))
                {
                    throw new InvalidOperationException($"Runtime request media types differ for {operation.Key} {path.Key}.");
                }
                if (actualMedia.Contains("application/json"))
                {
                    ValidateShape(operation.Value.RequestBody!.Content!["application/json"].Schema!,
                        expected.RequestBody!.Content!["application/json"].Schema!, path.Key);
                }
            }
        }

        // Minimal API inference cannot describe filters, merge-patch fields or manually written headers.
        // Runtime payload and CLR schema compatibility are checked independently by contract tests.
        document.Info = reviewed.Info;
        document.Servers = reviewed.Servers;
        document.Paths = reviewed.Paths;
        document.Components = reviewed.Components;
        document.Tags = reviewed.Tags;
    }

    private static Dictionary<string, IOpenApiSchema> Properties(IOpenApiSchema schema)
    {
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        foreach (var parent in schema.AllOf ?? [])
        {
            foreach (var property in Properties(parent))
            {
                properties[property.Key] = property.Value;
            }
        }
        foreach (var property in schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
        {
            properties[property.Key] = property.Value;
        }
        return properties;
    }

    private static void ValidateShape(IOpenApiSchema actual, IOpenApiSchema expected, string path)
    {
        actual = NonNullSchema(actual);
        expected = NonNullSchema(expected);
        // Validation refines binder nullability/required fields; their exact rejection matrices run over HTTP.
        var actualType = (actual.Type ?? 0) & ~JsonSchemaType.Null;
        var expectedType = (expected.Type ?? 0) & ~JsonSchemaType.Null;
        if (actualType != 0 && expectedType != 0 && actualType != expectedType
            || actual.Format is not null && expected.Format is not null && actual.Format != expected.Format)
        {
            throw new InvalidOperationException($"Runtime request schema type/format differs at {path}: {actual.Type}/{actual.Format} versus {expected.Type}/{expected.Format}.");
        }
        var actualProperties = Properties(actual);
        var expectedProperties = Properties(expected);
        if (!actualProperties.Keys.Order().SequenceEqual(expectedProperties.Keys.Order()))
        {
            throw new InvalidOperationException($"Runtime request schema fields differ at {path}.");
        }
        foreach (var property in expectedProperties)
        {
            ValidateShape(actualProperties[property.Key], property.Value, path + "." + property.Key);
        }
        if (actual.Items is not null && expected.Items is not null)
        {
            ValidateShape(actual.Items, expected.Items, path + "[]");
        }
    }

    private static IOpenApiSchema NonNullSchema(IOpenApiSchema schema)
    {
        var alternatives = schema.AnyOf ?? schema.OneOf;
        if (alternatives is { Count: 2 } && alternatives.Any(x => x.Type == JsonSchemaType.Null))
        {
            return alternatives.Single(x => x.Type != JsonSchemaType.Null);
        }
        return schema;
    }
}
