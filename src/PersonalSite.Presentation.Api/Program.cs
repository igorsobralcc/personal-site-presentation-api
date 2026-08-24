using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;
using PersonalSite.Presentation.Api.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<PresentationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Presentation"), npgsql =>
        npgsql.MigrationsHistoryTable("__ef_migrations_history", PresentationDbContext.Schema))
        .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks()
    .AddCheck<PresentationDatabaseHealthCheck>("presentation_database", tags: ["ready"]);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentSeedData.SeedAsync(scope.ServiceProvider.GetRequiredService<PresentationDbContext>());
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", checks = Array.Empty<object>() }))
    .AllowAnonymous();
app.MapGet("/health/ready", async (HealthCheckService healthChecks, HttpContext http, CancellationToken ct) =>
{
    var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), ct);
    http.Response.StatusCode = report.Status == HealthStatus.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
    await HealthResponseWriter.WriteAsync(http, report);
}).AllowAnonymous();

app.MapPresentationApi();
app.Run();

public partial class Program;
