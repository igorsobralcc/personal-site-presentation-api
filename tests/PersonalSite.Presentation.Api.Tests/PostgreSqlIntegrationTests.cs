using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PersonalSite.Presentation.Api.Data;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

[Collection("PostgreSQL")]
public sealed class PostgreSqlIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PF-029")]
    [Trait("Spec", "PF-030")]
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

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PF-016")]
    [Trait("Spec", "SC-005")]
    [Trait("Spec", "TE-004")]
    public async Task PostgreSql_enforces_partial_uniqueness_and_concurrency_tokens()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var options = Options(connection);
        await using var first = new PresentationDbContext(options);
        await using var second = new PresentationDbContext(options);
        await first.Database.UseTransactionAsync(transaction);
        await second.Database.UseTransactionAsync(transaction);

        var technology = new Technology { Name = "Race", NormalizedName = "RACE" };
        first.Technologies.Add(technology);
        await first.SaveChangesAsync();
        first.ChangeTracker.Clear();
        var left = await first.Technologies.SingleAsync(x => x.Id == technology.Id);
        var right = await second.Technologies.SingleAsync(x => x.Id == technology.Id);
        left.Name = "Winner";
        left.NormalizedName = "WINNER";
        left.Version++;
        await first.SaveChangesAsync();
        right.Name = "Loser";
        right.NormalizedName = "LOSER";
        right.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        first.ChangeTracker.Clear();
        first.Technologies.Add(new Technology { Name = "Winner duplicate", NormalizedName = "WINNER" });
        await Assert.ThrowsAsync<DbUpdateException>(() => first.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PF-022")]
    [Trait("Spec", "SD-009")]
    public async Task Failed_seed_insert_rolls_back_every_table()
    {
        var connectionString = RequiredConnectionString();
        var migrationOptions = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options;
        await using (var migrationDb = new PresentationDbContext(migrationOptions))
        {
            await migrationDb.Database.MigrateAsync();
            await migrationDb.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE presentation.profile_social_links, presentation.experience_highlights, presentation.experience_technologies, presentation.project_technologies, presentation.skills, presentation.profiles, presentation.experiences, presentation.projects, presentation.skill_categories, presentation.technologies RESTART IDENTITY");
        }

        var failingOptions = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new FailFirstInsertInterceptor()).Options;
        await using (var failingDb = new PresentationDbContext(failingOptions))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeedData.SeedAsync(failingDb));
        }

        await using var verification = new PresentationDbContext(migrationOptions);
        Assert.Equal(0, await verification.Profiles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verification.Experiences.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verification.Projects.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verification.SkillCategories.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verification.Technologies.IgnoreQueryFilters().CountAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PR-008")]
    [Trait("Spec", "SK-006")]
    [Trait("Spec", "SK-017")]
    public async Task Singleton_and_skill_unique_constraints_resolve_write_races_with_one_winner()
    {
        await using var connection = await OpenConnectionAsync();
        await ResetAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();
        var options = Options(connection);
        await using var db = new PresentationDbContext(options);
        await db.Database.UseTransactionAsync(transaction);
        var category = new SkillCategory { Name = "Category", NormalizedName = "CATEGORY" };
        db.SkillCategories.Add(category);
        db.Profiles.Add(new Profile { FullName = "Winner", Headline = "Winner", Biography = "Winner" });
        db.Skills.Add(new Skill { Name = "Winner", NormalizedName = "WINNER", CategoryId = category.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        db.Profiles.Add(new Profile { FullName = "Loser", Headline = "Loser", Biography = "Loser" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Skills.Add(new Skill { Name = "winner", NormalizedName = "WINNER", CategoryId = category.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PR-016")]
    [Trait("Spec", "EX-015")]
    [Trait("Spec", "PJ-015")]
    public async Task Every_aggregate_concurrency_token_prevents_a_lost_update()
    {
        await using var connection = await OpenConnectionAsync();
        await ResetAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();
        var options = Options(connection);
        await using (var setup = new PresentationDbContext(options))
        {
            await setup.Database.UseTransactionAsync(transaction);
            setup.AddRange(
                new Profile { FullName = "Profile", Headline = "Headline", Biography = "Biography" },
                new Experience { Company = "Company", Role = "Role", Summary = "Summary", StartDate = new(2020, 1, 1) },
                new Project { Name = "Project", Summary = "Summary" });
            await setup.SaveChangesAsync();
        }

        await AssertConcurrentUpdateAsync<Profile>(connection, transaction, value => value.FullName += " winner");
        await AssertConcurrentUpdateAsync<Experience>(connection, transaction, value => value.Company += " winner");
        await AssertConcurrentUpdateAsync<Project>(connection, transaction, value => value.Name += " winner");
        await transaction.RollbackAsync();
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "PR-017")]
    [Trait("Spec", "EX-023")]
    [Trait("Spec", "PJ-022")]
    public async Task Failed_child_replacements_roll_back_root_and_owned_rows()
    {
        var connectionString = RequiredConnectionString();
        await using (var connection = await OpenConnectionAsync())
        {
            await ResetAsync(connection);
        }
        var normal = Options(connectionString);
        Guid profileId;
        Guid experienceId;
        Guid projectId;
        Guid technologyId;
        await using (var setup = new PresentationDbContext(normal))
        {
            var profile = new Profile
            {
                FullName = "Original", Headline = "Headline", Biography = "Biography",
                SocialLinks = [new ProfileSocialLink { Label = "Old", Url = "https://example.com/old" }]
            };
            var experience = new Experience
            {
                Company = "Original", Role = "Role", Summary = "Summary", StartDate = new(2020, 1, 1),
                Highlights = [new ExperienceHighlight { Text = "Old" }]
            };
            var technology = new Technology { Name = "Technology", NormalizedName = "TECHNOLOGY" };
            var project = new Project
            {
                Name = "Original", Summary = "Summary",
                Technologies = [new ProjectTechnology { TechnologyId = technology.Id }]
            };
            setup.AddRange(profile, experience, technology, project);
            await setup.SaveChangesAsync();
            (profileId, experienceId, projectId, technologyId) =
                (profile.Id, experience.Id, project.Id, technology.Id);
        }

        await AssertReplacementRollbackAsync(connectionString, "profile_social_links", async db =>
        {
            var value = await db.Profiles.Include(x => x.SocialLinks).SingleAsync(x => x.Id == profileId);
            db.ProfileSocialLinks.RemoveRange(value.SocialLinks);
            db.ProfileSocialLinks.Add(new ProfileSocialLink
                { ProfileId = value.Id, Label = "New", Url = "https://example.com/new" });
            value.FullName = "Changed";
            value.Version++;
        });
        await AssertReplacementRollbackAsync(connectionString, "experience_highlights", async db =>
        {
            var value = await db.Experiences.Include(x => x.Highlights).SingleAsync(x => x.Id == experienceId);
            db.ExperienceHighlights.RemoveRange(value.Highlights);
            db.ExperienceHighlights.Add(new ExperienceHighlight { ExperienceId = value.Id, Text = "New" });
            value.Company = "Changed";
            value.Version++;
        });
        await AssertReplacementRollbackAsync(connectionString, "project_technologies", async db =>
        {
            var value = await db.Projects.Include(x => x.Technologies).SingleAsync(x => x.Id == projectId);
            db.ProjectTechnologies.RemoveRange(value.Technologies);
            var replacement = new Technology { Name = "Replacement", NormalizedName = "REPLACEMENT" };
            db.Technologies.Add(replacement);
            db.ProjectTechnologies.Add(new ProjectTechnology { ProjectId = value.Id, TechnologyId = replacement.Id });
            value.Name = "Changed";
            value.Version++;
        });

        await using var verify = new PresentationDbContext(normal);
        Assert.Equal("Original", (await verify.Profiles.SingleAsync(x => x.Id == profileId)).FullName);
        Assert.Equal("Old", (await verify.ProfileSocialLinks.SingleAsync(x => x.ProfileId == profileId)).Label);
        Assert.Equal("Original", (await verify.Experiences.SingleAsync(x => x.Id == experienceId)).Company);
        Assert.Equal("Old", (await verify.ExperienceHighlights.SingleAsync(x => x.ExperienceId == experienceId)).Text);
        Assert.Equal("Original", (await verify.Projects.SingleAsync(x => x.Id == projectId)).Name);
        Assert.Equal(technologyId, (await verify.ProjectTechnologies.SingleAsync(x => x.ProjectId == projectId)).TechnologyId);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "SD-010")]
    public async Task Failed_seed_commit_rolls_back_the_complete_dataset()
    {
        var connectionString = RequiredConnectionString();
        await using (var connection = await OpenConnectionAsync())
        {
            await ResetAsync(connection);
        }
        var failing = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention().AddInterceptors(new FailCommitInterceptor()).Options;
        await using (var db = new PresentationDbContext(failing))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeedData.SeedAsync(db));
        }
        await using var verify = new PresentationDbContext(Options(connectionString));
        Assert.Equal(0, await verify.Profiles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verify.Technologies.IgnoreQueryFilters().CountAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "SD-011")]
    public async Task Concurrent_seeders_leave_exactly_one_complete_dataset()
    {
        var connectionString = RequiredConnectionString();
        await using (var connection = await OpenConnectionAsync())
        {
            await ResetAsync(connection);
        }
        var options = Options(connectionString);
        await using var firstDb = new PresentationDbContext(options);
        await using var secondDb = new PresentationDbContext(options);
        var first = DevelopmentSeedData.SeedAsync(firstDb);
        var second = DevelopmentSeedData.SeedAsync(secondDb);
        try
        {
            await Task.WhenAll(first, second);
        }
        catch (Exception)
        {
            // One serializable transaction may be selected as the loser.
        }
        Assert.True(first.IsCompletedSuccessfully || second.IsCompletedSuccessfully);
        await using var verify = new PresentationDbContext(options);
        Assert.Equal(1, await verify.Profiles.CountAsync());
        Assert.Equal(5, await verify.Experiences.CountAsync());
        Assert.Equal(4, await verify.Projects.CountAsync());
        Assert.Equal(21, await verify.Technologies.CountAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    [Trait("Spec", "EX-014")]
    [Trait("Spec", "PJ-014")]
    public async Task Technology_delete_and_aggregate_create_never_both_succeed()
    {
        var connectionString = RequiredConnectionString();
        await using var factory = new PostgreSqlApiFactory(connectionString);
        using var client = factory.CreateApiClient();
        foreach (var resource in new[] { "experiences", "projects" })
        {
            await using (var connection = await OpenConnectionAsync())
            {
                await ResetAsync(connection);
            }
            var technologyResponse = await client.PostAsJsonAsync("/api/v1/admin/technologies",
                new { name = $"Technology for {resource}" });
            var technologyId = (await technologyResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            var createTask = resource == "experiences"
                ? client.PostAsJsonAsync("/api/v1/admin/experiences",
                    FlowTestSupport.Experience(technologyIds: [technologyId]))
                : client.PostAsJsonAsync("/api/v1/admin/projects",
                    FlowTestSupport.Project(technologyIds: [technologyId]));
            using var deleteRequest = FlowTestSupport.Delete(
                $"/api/v1/admin/technologies/{technologyId}", technologyResponse.Headers.ETag!.Tag);
            var deleteTask = client.SendAsync(deleteRequest);
            await Task.WhenAll(createTask, deleteTask);

            var createStatus = createTask.Result.StatusCode;
            var deleteStatus = deleteTask.Result.StatusCode;
            Assert.True(
                (createStatus == HttpStatusCode.Created && deleteStatus == HttpStatusCode.Conflict)
                || (createStatus == HttpStatusCode.BadRequest && deleteStatus == HttpStatusCode.NoContent),
                $"Unexpected race outcome for {resource}: create {createStatus}, delete {deleteStatus}.");
        }
    }

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable("PRESENTATION_TEST_CONNECTION_STRING")!;

    private static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(RequiredConnectionString());
        await connection.OpenAsync();
        var options = Options(connection);
        await using var db = new PresentationDbContext(options);
        await db.Database.MigrateAsync();
        return connection;
    }

    private static DbContextOptions<PresentationDbContext> Options(DbConnection connection) =>
        new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options;

    private static DbContextOptions<PresentationDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", PresentationDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options;

    private static async Task ResetAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE presentation.profile_social_links, presentation.experience_highlights, presentation.experience_technologies, presentation.project_technologies, presentation.skills, presentation.profiles, presentation.experiences, presentation.projects, presentation.skill_categories, presentation.technologies RESTART IDENTITY",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertConcurrentUpdateAsync<TEntity>(NpgsqlConnection connection,
        DbTransaction transaction, Action<TEntity> mutate) where TEntity : ManagedEntity
    {
        var options = Options(connection);
        await using var leftDb = new PresentationDbContext(options);
        await using var rightDb = new PresentationDbContext(options);
        await leftDb.Database.UseTransactionAsync(transaction);
        await rightDb.Database.UseTransactionAsync(transaction);
        var left = await leftDb.Set<TEntity>().SingleAsync();
        var right = await rightDb.Set<TEntity>().SingleAsync();
        mutate(left);
        left.Version++;
        await leftDb.SaveChangesAsync();
        mutate(right);
        right.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => rightDb.SaveChangesAsync());
    }

    private static async Task AssertReplacementRollbackAsync(string connectionString, string tableFragment,
        Func<PresentationDbContext, Task> mutate)
    {
        var options = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
            .AddInterceptors(new FailCommandContainingInterceptor(tableFragment)).Options;
        await using var db = new PresentationDbContext(options);
        await mutate(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}

[CollectionDefinition("PostgreSQL", DisableParallelization = true)]
public sealed class PostgreSqlCollection;

internal sealed class FailFirstInsertInterceptor : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Forced seed insert failure.");
        }

        return ValueTask.FromResult(result);
    }
}

internal sealed class FailCommandContainingInterceptor(string fragment) : DbCommandInterceptor
{
    private void ThrowIfMatched(DbCommand command)
    {
        if (command.CommandText.Contains(fragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Forced command failure for {fragment}.");
        }
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfMatched(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfMatched(command);
        return ValueTask.FromResult(result);
    }
}

internal sealed class FailCommitInterceptor : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<InterceptionResult>(new InvalidOperationException("Forced commit failure."));
}

internal sealed class PostgreSqlApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Presentation", connectionString);
        builder.UseSetting("Admin:Key", "integration-secret");
        builder.UseSetting("SeedData:Enabled", "false");
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-secret");
        return client;
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRESENTATION_TEST_CONNECTION_STRING")))
        {
            Skip = "Set PRESENTATION_TEST_CONNECTION_STRING to a disposable PostgreSQL database to run persistence verification.";
        }
    }
}
