using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public static class PublicPresentationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/presentation", Get).AllowAnonymous();
    }

    private static async Task<IResult> Get(PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var profile = await db.Profiles.Include(x => x.SocialLinks).SingleOrDefaultAsync(ct);
        if (profile is null)
        {
            return ApiProblems.Create(http, 404, "Presentation not found", "The profile has not been initialized.");
        }

        var experiences = await db.Experiences.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.EndDate == null).ThenByDescending(x => x.EndDate).ThenBy(x => x.Id).ToListAsync(ct);
        var projects = await db.Projects.Where(x => x.IsFeatured).Include(x => x.Technologies).ThenInclude(x => x.Technology).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var categories = await db.SkillCategories.Include(x => x.Skills).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

        var timestamps = new List<DateTimeOffset> { profile.PublicUpdatedAt };
        timestamps.AddRange(experiences.Select(x => x.PublicUpdatedAt));
        timestamps.AddRange(projects.Select(x => x.PublicUpdatedAt));
        timestamps.AddRange(categories.Select(x => x.PublicUpdatedAt));
        timestamps.AddRange(categories.SelectMany(x => x.Skills).Select(x => x.PublicUpdatedAt));
        timestamps.AddRange(projects.SelectMany(x => x.Technologies).Select(x => x.Technology.PublicUpdatedAt));
        var response = new PublicPresentation(
            new PublicProfile(profile.Id, profile.FullName, profile.Headline, profile.Biography, profile.ShortSummary, profile.Location, profile.Email, profile.Availability, profile.CurrentFocus,
                profile.SocialLinks.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new SocialLinkResponse(x.Label, x.Url)).ToList()),
            experiences.Select(x => new PublicExperience(x.Id, x.Company, x.Role, x.StartDate, x.EndDate, x.Summary)).ToList(),
            projects.Select(x => new PublicProject(x.Id, x.Name, x.Summary, x.RepositoryUrl, x.LiveUrl,
                x.Technologies.OrderBy(y => y.Technology.Name).ThenBy(y => y.TechnologyId).Select(y => new TechnologySummary(y.TechnologyId, y.Technology.Name)).ToList(),
                x.ImageUrl is null ? null : new ProjectImageResponse(x.ImageUrl, x.ImageAlt!, x.ImageWidth!.Value, x.ImageHeight!.Value))).ToList(),
            categories.Select(x => new PublicSkillCategory(x.Id, x.Name, x.Skills.OrderBy(y => y.CreatedAt).ThenBy(y => y.Id).Select(y => new PublicSkill(y.Id, y.Name)).ToList())).ToList(),
            timestamps.Max());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}\"";
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = "public,max-age=60,must-revalidate";
        if (http.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, etag, StringComparison.Ordinal)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Json(response, JsonOptions);
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record PublicPresentation(PublicProfile Profile, IReadOnlyList<PublicExperience> Experiences, IReadOnlyList<PublicProject> Projects, IReadOnlyList<PublicSkillCategory> SkillCategories, DateTimeOffset UpdatedAt);
public sealed record PublicProfile(Guid Id, string FullName, string Headline, string Biography, string? ShortSummary, string? Location, string? Email, string? Availability, string? CurrentFocus, IReadOnlyList<SocialLinkResponse> SocialLinks);
public sealed record PublicExperience(Guid Id, string Company, string Role, DateOnly StartDate, DateOnly? EndDate, string Summary);
public sealed record PublicProject(Guid Id, string Name, string Summary, string? RepositoryUrl, string? LiveUrl, IReadOnlyList<TechnologySummary> Technologies, ProjectImageResponse? Image);
public sealed record TechnologySummary(Guid Id, string Name);
public sealed record PublicSkillCategory(Guid Id, string Name, IReadOnlyList<PublicSkill> Skills);
public sealed record PublicSkill(Guid Id, string Name);
