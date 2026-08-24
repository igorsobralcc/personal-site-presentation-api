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
    public async Task Seeds_the_complete_resume_dataset_once()
    {
        await using var db = CreateDatabase();
        await DevelopmentSeedData.SeedAsync(db);

        var profile = await db.Profiles.Include(x => x.SocialLinks).SingleAsync();
        Assert.Equal("Igor Sobral", profile.FullName);
        Assert.Equal("igorsobral.cc@gmail.com", profile.Email);
        Assert.Equal("LinkedIn", Assert.Single(profile.SocialLinks).Label);
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
            Assert.Empty(InputValidation.Experience(new ExperienceRequest(experience.Company, experience.Role, experience.Location,
                experience.StartDate, experience.EndDate, experience.Summary, experience.Highlights.Select(x => x.Text).ToList(),
                experience.Technologies.Select(x => x.TechnologyId).ToList())));
        foreach (var project in await db.Projects.Include(x => x.Technologies).ToListAsync())
            Assert.Empty(InputValidation.Project(new ProjectRequest(project.Name, project.Summary, project.RepositoryUrl,
                project.LiveUrl, project.Technologies.Select(x => x.TechnologyId).ToList(), project.IsFeatured, null)));

        var identifiers = await db.Technologies.Select(x => x.Id).OrderBy(x => x).ToArrayAsync();
        await DevelopmentSeedData.SeedAsync(db);
        Assert.Equal(identifiers, await db.Technologies.Select(x => x.Id).OrderBy(x => x).ToArrayAsync());
        Assert.Equal(1, await db.Profiles.CountAsync());
    }

    [Fact]
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
    public async Task Development_configuration_seeds_public_content_in_expected_order()
    {
        await using var factory = new ApiFactory(seedDataEnabled: true);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/v1/presentation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Self-Employed", body.GetProperty("experiences")[0].GetProperty("company").GetString());
        Assert.Equal("Automotive Operations Platform", body.GetProperty("projects")[0].GetProperty("name").GetString());
        Assert.Equal("Backend Engineering", body.GetProperty("skillCategories")[0].GetProperty("name").GetString());
        Assert.Equal("C#", body.GetProperty("skillCategories")[0].GetProperty("skills")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Production_never_runs_the_development_seeder()
    {
        await using var factory = new ApiFactory(seedDataEnabled: true, environment: "Production");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/presentation")).StatusCode);
    }

    private static PresentationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<PresentationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new PresentationDbContext(options);
    }
}
