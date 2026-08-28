using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PersonalSite.Presentation.Api.Common;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class DatabaseExceptionHandlerTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)]
    [Trait("Spec", "PF-020")]
    public async Task Unique_and_foreign_key_database_failures_are_traceable_conflicts(string sqlState)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "trace-for-test"
        };
        context.Response.Body = new MemoryStream();
        var postgres = new PostgresException("persistence conflict", "ERROR", "ERROR", sqlState);
        var exception = new DbUpdateException("write failed", postgres);

        Assert.True(await new DatabaseExceptionHandler().TryHandleAsync(context, exception, default));
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        Assert.Equal("Persistence conflict", body.GetProperty("title").GetString());
        Assert.Equal("trace-for-test", body.GetProperty("traceId").GetString());
        Assert.DoesNotContain(sqlState, body.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
