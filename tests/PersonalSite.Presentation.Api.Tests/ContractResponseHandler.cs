using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

/// <summary>Checks real HTTP responses independently of contract-backed OpenAPI generation.</summary>
internal sealed class ContractResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "openapi.json"), cancellationToken));
        var path = request.RequestUri!.AbsolutePath;
        var operation = document.RootElement.GetProperty("paths").EnumerateObject().FirstOrDefault(x =>
            Regex.IsMatch(path, "^" + Regex.Escape(x.Name).Replace("\\{id}", "[^/]+", StringComparison.Ordinal) + "$"));
        if (operation.Value.ValueKind == JsonValueKind.Undefined)
        {
            return response;
        }
        var method = request.Method.Method.ToLowerInvariant();
        if (!operation.Value.TryGetProperty(method, out var methodContract))
        {
            return response;
        }
        var status = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(methodContract.GetProperty("responses").TryGetProperty(status, out var result),
            $"Undocumented response: {method} {operation.Name} {status}.");
        result = JsonSchemaAssertions.Resolve(result, document.RootElement);
        if (result.TryGetProperty("headers", out var headers))
        {
            foreach (var header in headers.EnumerateObject())
            {
                Assert.True(response.Headers.Contains(header.Name) || response.Content.Headers.Contains(header.Name),
                    $"Missing response header: {method} {operation.Name} {status} {header.Name}.");
            }
        }
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (result.TryGetProperty("content", out var content))
        {
            var media = response.Content.Headers.ContentType?.MediaType;
            Assert.NotNull(media);
            Assert.True(content.TryGetProperty(media, out var mediaContract), $"Undocumented media type: {media}.");
            using var payload = JsonDocument.Parse(bytes);
            JsonSchemaAssertions.Validate(payload.RootElement, mediaContract.GetProperty("schema"), document.RootElement);
        }
        else
        {
            Assert.Empty(bytes);
        }
        return response;
    }
}
