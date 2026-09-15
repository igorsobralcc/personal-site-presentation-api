using System.Text.Json;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

internal static class JsonSchemaAssertions
{
    public static JsonElement Resolve(JsonElement node, JsonElement document)
    {
        while (node.TryGetProperty("$ref", out var reference))
        {
            var target = document;
            foreach (var segment in reference.GetString()!.Split('/').Skip(1))
            {
                target = target.GetProperty(segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
            }
            node = target;
        }
        return node;
    }

    public static void Validate(JsonElement value, JsonElement schema, JsonElement document)
    {
        schema = Resolve(schema, document);
        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (schema.TryGetProperty(keyword, out var alternatives))
            {
                var accepted = 0;
                foreach (var alternative in alternatives.EnumerateArray())
                {
                    try
                    {
                        Validate(value, alternative, document);
                        accepted++;
                    }
                    catch (Xunit.Sdk.XunitException)
                    {
                        // A union candidate may reject the value; another candidate must accept it.
                    }
                }
                Assert.True(keyword == "oneOf" ? accepted == 1 : accepted > 0, $"No valid {keyword} response schema match.");
            }
        }
        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var child in allOf.EnumerateArray())
            {
                Validate(value, child, document);
            }
        }
        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(x => x.GetString()).ToArray() : [type.GetString()];
            var actual = value.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.Number => value.TryGetInt64(out _) ? "integer" : "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                _ => "undefined"
            };
            Assert.True(types.Contains(actual) || actual == "integer" && types.Contains("number"),
                $"Schema type {type} does not accept {actual}.");
        }
        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var name in required.EnumerateArray())
                {
                    Assert.True(value.TryGetProperty(name.GetString()!, out _), $"Required response field is missing: {name}.");
                }
            }
            if (schema.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (value.TryGetProperty(property.Name, out var child))
                    {
                        Validate(child, property.Value, document);
                    }
                }
                if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False)
                {
                    foreach (var property in value.EnumerateObject())
                    {
                        Assert.True(properties.TryGetProperty(property.Name, out _), $"Unexpected public field: {property.Name}.");
                    }
                }
            }
            if (schema.TryGetProperty("additionalProperties", out var additionalSchema)
                && additionalSchema.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (!schema.TryGetProperty("properties", out var declared) || !declared.TryGetProperty(property.Name, out _))
                    {
                        Validate(property.Value, additionalSchema, document);
                    }
                }
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("maxItems", out var maxItems))
            {
                Assert.True(value.GetArrayLength() <= maxItems.GetInt32());
            }
            if (schema.TryGetProperty("minItems", out var minItems))
            {
                Assert.True(value.GetArrayLength() >= minItems.GetInt32());
            }
            if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
            {
                Assert.Equal(value.GetArrayLength(), value.EnumerateArray().Select(x => x.GetRawText()).Distinct().Count());
            }
            if (schema.TryGetProperty("items", out var items))
            {
                foreach (var child in value.EnumerateArray())
                {
                    Validate(child, items, document);
                }
            }
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            if (schema.TryGetProperty("minLength", out var minLength))
            {
                Assert.True(value.GetString()!.Length >= minLength.GetInt32());
            }
            if (schema.TryGetProperty("maxLength", out var maxLength))
            {
                Assert.True(value.GetString()!.Length <= maxLength.GetInt32());
            }
            if (schema.TryGetProperty("format", out var format))
            {
                switch (format.GetString())
                {
                    case "uuid":
                        Assert.True(Guid.TryParse(value.GetString(), out _));
                        break;
                    case "date":
                        Assert.True(DateOnly.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture, out _));
                        break;
                    case "date-time":
                        Assert.True(DateTimeOffset.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture, out _));
                        break;
                    case "uri":
                        Assert.True(Uri.TryCreate(value.GetString(), UriKind.Absolute, out _));
                        break;
                }
            }
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (schema.TryGetProperty("minimum", out var minimum))
            {
                Assert.True(value.GetDouble() >= minimum.GetDouble());
            }
            if (schema.TryGetProperty("maximum", out var maximum))
            {
                Assert.True(value.GetDouble() <= maximum.GetDouble());
            }
        }
    }
}
