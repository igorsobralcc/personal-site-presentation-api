using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class PostgreSqlIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Migrations_are_schema_isolated_and_foreign_keys_never_cascade()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRESENTATION_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connectionString!, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options;
        await using var db = new PresentationDbContext(options);
        await db.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tables = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'presentation' AND table_name IN ('profiles', 'experiences', 'projects', 'skill_categories', 'skills', 'technologies', '__ef_migrations_history')", connection);
        Assert.Equal(7L, (long)(await tables.ExecuteScalarAsync())!);
        await using var cascades = new NpgsqlCommand("SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE n.nspname = 'presentation' AND c.contype = 'f' AND c.confdeltype <> 'r'", connection);
        Assert.Equal(0L, (long)(await cascades.ExecuteScalarAsync())!);
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRESENTATION_TEST_CONNECTION_STRING")))
            Skip = "Set PRESENTATION_TEST_CONNECTION_STRING to a disposable PostgreSQL database to run persistence verification.";
    }
}
