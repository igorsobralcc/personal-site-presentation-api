using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public static class ProjectEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/projects");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapGet("/{id:guid}", Get);
        group.MapMethods("/{id:guid}", ["PATCH"], Patch).Accepts<JsonElement>("application/merge-patch+json");
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/restore", Restore);
    }
    private static async Task<IResult> List(PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!PageRequest.TryRead(http.Request, out var page, out var errors))
        {
            return ApiProblems.Validation(http, errors);
        }

        var query = page.IncludeDeleted ? db.Projects.IgnoreQueryFilters() : db.Projects;
        var count = await query.CountAsync(ct);
        var values = await query.Include(x => x.Technologies).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<ProjectResponse>(values.Select(x => x.ToResponse()).ToList(), page.Page, page.PageSize, count, (int)Math.Ceiling(count / (double)page.PageSize)));
    }
    private static async Task<IResult> Get(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await db.Projects.Include(x => x.Technologies).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null)
        {
            return NotFound(http);
        }

        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(value.ToResponse());
    }
    private static async Task<IResult> Create(ProjectRequest request, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var errors = InputValidation.Project(request);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(http, errors);
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        if (!await ExperienceEndpoints.TechnologyIdsAreActive(db, request.TechnologyIds!, ct))
        {
            return TechnologyError(http);
        }

        var value = Build(request);
        db.Projects.Add(value);
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Created($"/api/v1/admin/projects/{value.Id}", value.ToResponse());
    }
    private static async Task<IResult> Patch(Guid id, JsonElement document, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return ApiProblems.Validation(http, new()
            {
                ["document"] = ["A JSON object is required."]
            });
        }

        var value = await db.Projects.Include(x => x.Technologies).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null)
        {
            return NotFound(http);
        }

        var precondition = HttpConcurrency.Validate(http, value);
        if (precondition is not null)
        {
            return precondition;
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var patch = new MergePatch(document);
        ProjectImageRequest? currentImage = value.ImageUrl is null ? null : new(value.ImageUrl, value.ImageAlt, value.ImageWidth, value.ImageHeight);
        var request = new ProjectRequest(patch.Has("name") ? patch.Read<string>("name") : value.Name, patch.Has("summary") ? patch.Read<string>("summary") : value.Summary,
            patch.Has("repositoryUrl") ? patch.Read<string?>("repositoryUrl") : value.RepositoryUrl, patch.Has("liveUrl") ? patch.Read<string?>("liveUrl") : value.LiveUrl,
            patch.Has("technologyIds") ? patch.Read<List<Guid>>("technologyIds") : value.Technologies.Select(x => x.TechnologyId).ToList(), patch.Has("isFeatured") ? patch.Read<bool?>("isFeatured") : value.IsFeatured,
            patch.Has("image") ? patch.Read<ProjectImageRequest?>("image") : currentImage);
        var errors = InputValidation.Project(request);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(http, errors);
        }

        if (!await ExperienceEndpoints.TechnologyIdsAreActive(db, request.TechnologyIds!, ct))
        {
            return TechnologyError(http);
        }

        Apply(value, request);
        if (patch.Has("technologyIds"))
        {
            db.ProjectTechnologies.RemoveRange(value.Technologies.ToList());
            db.ProjectTechnologies.AddRange(request.TechnologyIds!.Select(x => new ProjectTechnology { ProjectId = value.Id, TechnologyId = x }));
        }
        value.Version++;
        value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(value.ToResponse());
    }
    private static async Task<IResult> Delete(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await db.Projects.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null)
        {
            return NotFound(http);
        }

        if (!http.Request.Headers.ContainsKey("If-Match"))
        {
            return ApiProblems.Create(http, 428, "Precondition Required", "If-Match is required.");
        }

        if (value.DeletedAt is not null)
        {
            return Results.NoContent();
        }

        var precondition = HttpConcurrency.Validate(http, value);
        if (precondition is not null)
        {
            return precondition;
        }

        value.DeletedAt = DateTimeOffset.UtcNow;
        value.Version++;
        value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.NoContent();
    }
    private static async Task<IResult> Restore(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await db.Projects.IgnoreQueryFilters().Include(x => x.Technologies).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null || value.DeletedAt is null)
        {
            return NotFound(http);
        }

        var precondition = HttpConcurrency.Validate(http, value);
        if (precondition is not null)
        {
            return precondition;
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        if (!await ExperienceEndpoints.TechnologyIdsAreActive(db, value.Technologies.Select(x => x.TechnologyId).ToList(), ct))
        {
            return ApiProblems.Create(http, 409, "Project references a deleted technology");
        }

        value.DeletedAt = null;
        value.Version++;
        value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.NoContent();
    }
    private static Project Build(ProjectRequest request)
    {
        var value = new Project { Name = request.Name!.Trim(), Summary = request.Summary!.Trim(), Technologies = request.TechnologyIds!.Select(x => new ProjectTechnology { TechnologyId = x }).ToList() };
        Apply(value, request);
        return value;
    }
    private static void Apply(Project value, ProjectRequest request)
    {
        value.Name = request.Name!.Trim();
        value.Summary = request.Summary!.Trim();
        value.RepositoryUrl = request.RepositoryUrl?.Trim();
        value.LiveUrl = request.LiveUrl?.Trim();
        value.IsFeatured = request.IsFeatured!.Value;
        value.ImageUrl = request.Image?.Url;
        value.ImageAlt = request.Image?.Alt?.Trim();
        value.ImageWidth = request.Image?.Width;
        value.ImageHeight = request.Image?.Height;
    }
    private static IResult TechnologyError(HttpContext http)
    {
        return ApiProblems.Validation(http, new()
        {
            ["technologyIds"] = ["Every identifier must reference an active technology."]
        });
    }

    private static IResult NotFound(HttpContext http)
    {
        return ApiProblems.Create(http, 404, "Project not found");
    }
}
