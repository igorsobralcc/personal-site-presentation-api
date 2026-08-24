using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class ContractTests
{
    [Fact]
    public async Task Generated_and_checked_in_contracts_expose_the_same_routes()
    {
        await using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generated = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths")
            .EnumerateObject().Select(x => x.Name).Order().ToArray();
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "openapi.yaml"));
        var checkedIn = Regex.Matches(await File.ReadAllTextAsync(contractPath), @"(?m)^  (?<path>/[^:]+):\s*$")
            .Select(x => x.Groups["path"].Value).Order().ToArray();
        Assert.Equal(checkedIn, generated);
    }
}
