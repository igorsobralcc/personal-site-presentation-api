using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Data;
using PersonalSite.Presentation.Api.Features;
using Xunit;

namespace PersonalSite.Presentation.Api.Tests;

public sealed class DevelopmentSeedDataTests
{
    [Fact]
    [Trait("Spec", "SD-001")]
    [Trait("Spec", "SD-002")]
    [Trait("Spec", "SD-013")]
    [Trait("Spec", "SD-014")]
    public async Task Seeds_the_complete_resume_dataset_once()
    {
        await using var db = CreateDatabase();
        await DevelopmentSeedData.SeedAsync(db);

        var profile = await db.Profiles.Include(x => x.SocialLinks).SingleAsync();
        Assert.Equal("Igor Sobral", profile.FullName);
        Assert.Equal("igorsobral.cc@gmail.com", profile.Email);
        Assert.Equal("Open to mid-level backend software engineering opportunities worldwide.", profile.Availability);
        Assert.Collection(profile.SocialLinks.OrderBy(x => x.CreatedAt),
            link => { Assert.Equal("LinkedIn", link.Label); Assert.Equal("https://www.linkedin.com/in/igor-sobral-m", link.Url); },
            link => { Assert.Equal("GitHub", link.Label); Assert.Equal("https://github.com/igorsobralcc", link.Url); });
        Assert.Equal(5, await db.Experiences.CountAsync());
        Assert.Equal(29, await db.ExperienceHighlights.CountAsync());
        Assert.Equal(24, await db.ExperienceTechnologies.CountAsync());
        Assert.Equal(4, await db.Projects.CountAsync());
        Assert.Equal(19, await db.ProjectTechnologies.CountAsync());
        Assert.Equal(5, await db.SkillCategories.CountAsync());
        Assert.Equal(23, await db.Skills.CountAsync());
        Assert.Equal(21, await db.Technologies.CountAsync());
        Assert.All(db.ChangeTracker.Entries<ManagedEntity>().Select(x => x.Entity), x => Assert.Equal(7, x.Id.Version));
        Assert.All(db.ChangeTracker.Entries<ManagedEntity>().Select(x => x.Entity), x => Assert.Equal(1, x.Version));

        Assert.Empty(InputValidation.Profile(new ProfileRequest(profile.FullName, profile.Headline, profile.Biography,
            profile.ShortSummary, profile.Location, profile.Email, profile.Availability, profile.CurrentFocus,
            profile.SocialLinks.Select(x => new SocialLinkRequest(x.Label, x.Url)).ToList())));
        foreach (var experience in await db.Experiences.Include(x => x.Highlights).Include(x => x.Technologies).ToListAsync())
        {
            Assert.Empty(InputValidation.Experience(new ExperienceRequest(experience.Company, experience.Role, experience.Location,
                experience.StartDate, experience.EndDate, experience.Summary, experience.Highlights.Select(x => x.Text).ToList(),
                experience.Technologies.Select(x => x.TechnologyId).ToList())));
        }

        foreach (var project in await db.Projects.Include(x => x.Technologies).ToListAsync())
        {
            Assert.Empty(InputValidation.Project(new ProjectRequest(project.Name, project.Summary, project.RepositoryUrl,
                project.LiveUrl, project.Technologies.Select(x => x.TechnologyId).ToList(), project.IsFeatured, null)));
        }

        var identifiers = await db.Technologies.Select(x => x.Id).OrderBy(x => x).ToArrayAsync();
        await DevelopmentSeedData.SeedAsync(db);
        Assert.Equal(identifiers, await db.Technologies.Select(x => x.Id).OrderBy(x => x).ToArrayAsync());
        Assert.Equal(1, await db.Profiles.CountAsync());
    }

    [Fact]
    [Trait("Spec", "SD-003")]
    [Trait("Spec", "SD-005")]
    public async Task Preserves_a_non_empty_database_without_supplementing_it()
    {
        await using var db = CreateDatabase();
        db.Technologies.Add(new Technology { Name = "Existing", NormalizedName = "EXISTING" });
        await db.SaveChangesAsync();

        await DevelopmentSeedData.SeedAsync(db);

        Assert.Equal("Existing", (await db.Technologies.SingleAsync()).Name);
        Assert.Empty(await db.Profiles.ToListAsync());
        Assert.Empty(await db.Experiences.ToListAsync());
        Assert.Empty(await db.Projects.ToListAsync());
        Assert.Empty(await db.SkillCategories.ToListAsync());
    }

    [Fact]
    [Trait("Spec", "SD-015")]
    public async Task Development_configuration_seeds_public_content_in_expected_order()
    {
        await using var factory = new ApiFactory(seedDataEnabled: true);
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        var response = await client.GetAsync("/api/v1/presentation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Self-Employed", body.GetProperty("experiences")[0].GetProperty("company").GetString());
        Assert.Equal("Automotive Operations Platform", body.GetProperty("projects")[0].GetProperty("name").GetString());
        Assert.Equal("Backend Engineering", body.GetProperty("skillCategories")[0].GetProperty("name").GetString());
        Assert.Equal("C#", body.GetProperty("skillCategories")[0].GetProperty("skills")[0].GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Spec", "SD-007")]
    public async Task Production_never_runs_the_development_seeder()
    {
        await using var factory = new ApiFactory(seedDataEnabled: true, environment: "Production");
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/presentation")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "SD-006")]
    public async Task Development_with_seed_disabled_does_not_initialize_content()
    {
        await using var factory = new ApiFactory(seedDataEnabled: false);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/presentation")).StatusCode);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("experience")]
    [InlineData("project")]
    [InlineData("category")]
    [InlineData("skill")]
    [InlineData("technology")]
    [Trait("Spec", "SD-004")]
    [Trait("Spec", "SD-005")]
    public async Task A_deleted_row_in_any_managed_table_prevents_all_seeding(string resource)
    {
        await using var db = CreateDatabase();
        var deletedAt = DateTimeOffset.UtcNow;
        switch (resource)
        {
            case "profile":
                db.Profiles.Add(new Profile
                {
                    FullName = "Deleted", Headline = "Deleted", Biography = "Deleted", DeletedAt = deletedAt
                });
                break;
            case "experience":
                db.Experiences.Add(new Experience
                {
                    Company = "Deleted", Role = "Deleted", Summary = "Deleted",
                    StartDate = new(2020, 1, 1), DeletedAt = deletedAt
                });
                break;
            case "project":
                db.Projects.Add(new Project
                {
                    Name = "Deleted", Summary = "Deleted", DeletedAt = deletedAt
                });
                break;
            case "category":
                db.SkillCategories.Add(new SkillCategory
                {
                    Name = "Deleted", NormalizedName = "DELETED", DeletedAt = deletedAt
                });
                break;
            case "skill":
                db.Skills.Add(new Skill
                {
                    Name = "Deleted", NormalizedName = "DELETED", CategoryId = Guid.NewGuid(),
                    DeletedAt = deletedAt
                });
                break;
            case "technology":
                db.Technologies.Add(new Technology
                {
                    Name = "Deleted", NormalizedName = "DELETED", DeletedAt = deletedAt
                });
                break;
        }
        await db.SaveChangesAsync();

        await DevelopmentSeedData.SeedAsync(db);

        var total = await db.Profiles.IgnoreQueryFilters().CountAsync()
            + await db.Experiences.IgnoreQueryFilters().CountAsync()
            + await db.Projects.IgnoreQueryFilters().CountAsync()
            + await db.SkillCategories.IgnoreQueryFilters().CountAsync()
            + await db.Skills.IgnoreQueryFilters().CountAsync()
            + await db.Technologies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(1, total);
    }

    [Fact]
    [Trait("Spec", "SD-008")]
    public async Task Relational_migration_failure_aborts_seeding()
    {
        var options = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=missing;Username=none;Password=secret;Timeout=1")
            .Options;
        await using var db = new PresentationDbContext(options);
        await Assert.ThrowsAnyAsync<Exception>(() => DevelopmentSeedData.SeedAsync(db));
    }

    [Fact]
    [Trait("Spec", "SD-012")]
    public async Task Precancelled_seed_operation_stops_without_inserting_content()
    {
        await using var db = CreateDatabase();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DevelopmentSeedData.SeedAsync(db, cancellation.Token));
        Assert.Equal(0, await db.Profiles.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    [Trait("Spec", "SD-016")]
    public async Task Seeder_source_and_seeded_values_contain_no_operational_secrets_or_local_paths()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "PersonalSite.Presentation.Api", "Data", "DevelopmentSeedData.cs"));
        var source = await File.ReadAllTextAsync(sourcePath);
        Assert.DoesNotContain("Admin:Key", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionStrings", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users\\", source, StringComparison.OrdinalIgnoreCase);

        await using var db = CreateDatabase();
        await DevelopmentSeedData.SeedAsync(db);
        var serialized = System.Text.Json.JsonSerializer.Serialize(await db.Profiles.ToListAsync());
        Assert.DoesNotContain("Password=", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static PresentationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new PresentationDbContext(options);
    }
}
