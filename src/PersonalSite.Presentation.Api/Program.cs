using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
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
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", checks = Array.Empty<object>() }))
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

app.MapPresentationApi();
app.Run();

public partial class Program;
