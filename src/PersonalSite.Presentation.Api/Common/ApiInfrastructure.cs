using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Common;

public sealed class AdminKeyFilter(IConfiguration configuration, IWebHostEnvironment environment) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!request.IsHttps && !environment.IsDevelopment())
        {
            return ApiProblems.Create(context.HttpContext, 400, "HTTPS required", "Management endpoints require HTTPS.");
        }

        var configured = configuration["Admin:Key"];
        var supplied = request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrEmpty(configured) || !FixedEquals(configured, supplied))
        {
            return ApiProblems.Create(context.HttpContext, 401, "Unauthorized", "A valid administrator key is required.");
        }

        return await next(context);
    }

    private static bool FixedEquals(string expected, string actual)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public sealed class OperationLoggingFilter(ILogger<OperationLoggingFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var started = Stopwatch.GetTimestamp();
        var http = context.HttpContext;
        try
        {
            var result = await next(context);
            var outcome = result is IStatusCodeHttpResult { StatusCode: { } statusCode } ? statusCode : http.Response.StatusCode;
            logger.LogInformation("Management operation {Operation} on {ResourceType} {ResourceId} completed with {Outcome} in {DurationMs}ms; trace {TraceId}",
                http.Request.Method, ResourceType(http.Request.Path), http.Request.RouteValues["id"], outcome,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, http.TraceIdentifier);
            return result;
        }
#pragma warning disable S2139 // Log operation context here while preserving exception types for the central handler.
        catch (Exception exception)
        {
            var concurrencyEntries = exception is DbUpdateConcurrencyException concurrency
                ? string.Join(',', concurrency.Entries.Select(x => x.Metadata.ClrType.Name))
                : null;
            logger.LogError(exception, "Management operation {Operation} on {ResourceType} {ResourceId} failed in {DurationMs}ms; trace {TraceId}",
                http.Request.Method, ResourceType(http.Request.Path), http.Request.RouteValues["id"],
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, http.TraceIdentifier);
            if (concurrencyEntries is not null)
            {
                logger.LogWarning("Concurrency conflict involved {EntityTypes}; trace {TraceId}", concurrencyEntries, http.TraceIdentifier);
            }

            throw;
        }
#pragma warning restore S2139
    }

    private static string ResourceType(PathString path)
    {
        return path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(3) ?? "unknown";
    }
}

public sealed class DatabaseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DbUpdateConcurrencyException => StatusCodes.Status412PreconditionFailed,
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } } => StatusCodes.Status409Conflict,
            _ => 0
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: status == 409 ? "Persistence conflict" : "Precondition Failed",
            extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier }).ExecuteAsync(httpContext);
        return true;
    }
}

public static class ApiProblems
{
    public static IResult Create(HttpContext http, int status, string title, string? detail = null)
    {
        return Results.Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier });
    }

    public static IResult Validation(HttpContext http, Dictionary<string, string[]> errors)
    {
        return Results.ValidationProblem(errors, statusCode: 400,
            extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier });
    }
}

public readonly record struct PageRequest(int Page, int PageSize, bool IncludeDeleted)
{
    public static bool TryRead(HttpRequest request, out PageRequest page, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var pageNumber = ParseInt(request, "X-Page", 1, 1, int.MaxValue, errors);
        var pageSize = ParseInt(request, "X-Page-Size", 20, 1, 100, errors);
        var includeDeleted = false;
        if (request.Headers.TryGetValue("X-Include-Deleted", out var raw) && !bool.TryParse(raw, out includeDeleted))
        {
            errors["X-Include-Deleted"] = ["Must be true or false."];
        }

        page = new(pageNumber, pageSize, includeDeleted);
        return errors.Count == 0;
    }

    private static int ParseInt(HttpRequest request, string name, int fallback, int min, int max, Dictionary<string, string[]> errors)
    {
        if (!request.Headers.TryGetValue(name, out var raw))
        {
            return fallback;
        }

        if (int.TryParse(raw, out var value) && value >= min && value <= max)
        {
            return value;
        }

        errors[name] = [$"Must be an integer between {min} and {max}."];
        return fallback;
    }
}

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public static class HttpConcurrency
{
    public static string ETag(long version)
    {
        return $"\"{version}\"";
    }

    public static IResult? Validate(HttpContext http, ManagedEntity entity)
    {
        if (!http.Request.Headers.TryGetValue("If-Match", out var value))
        {
            return ApiProblems.Create(http, 428, "Precondition Required", "If-Match is required.");
        }

        if (!string.Equals(value.ToString(), ETag(entity.Version), StringComparison.Ordinal))
        {
            return ApiProblems.Create(http, 412, "Precondition Failed", "The resource has changed.");
        }

        return null;
    }
    public static void Set(HttpResponse response, long version)
    {
        response.Headers.ETag = ETag(version);
    }
}

public sealed class PresentationDatabaseHealthCheck(PresentationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            return await db.Database.CanConnectAsync(timeout.Token) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Database unavailable.", exception); }
    }
}

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            checks = report.Entries.Select(x => new { name = x.Key, status = x.Value.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy" })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload), context.RequestAborted);
    }
}
