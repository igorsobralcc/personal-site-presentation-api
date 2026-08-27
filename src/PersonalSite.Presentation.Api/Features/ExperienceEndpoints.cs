using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public static class ExperienceEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/experiences");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapGet("/{id:guid}", Get);
        group.MapMethods("/{id:guid}", ["PATCH"], Patch).Accepts<JsonElement>("application/merge-patch+json");
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/restore", Restore);
    }
    private static IQueryable<Experience> Expanded(PresentationDbContext db)
    {
        return db.Experiences.Include(x => x.Highlights).Include(x => x.Technologies);
    }

    private static async Task<IResult> List(PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!PageRequest.TryRead(http.Request, out var page, out var errors))
        {
            return ApiProblems.Validation(http, errors);
        }

        var query = page.IncludeDeleted ? db.Experiences.IgnoreQueryFilters() : db.Experiences;
        var count = await query.CountAsync(ct);
        var values = await query.Include(x => x.Highlights).Include(x => x.Technologies).OrderByDescending(x => x.StartDate).ThenByDescending(x => x.EndDate == null).ThenByDescending(x => x.EndDate).ThenBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<ExperienceResponse>(values.Select(x => x.ToResponse()).ToList(), page.Page, page.PageSize, count, (int)Math.Ceiling(count / (double)page.PageSize)));
    }
    private static async Task<IResult> Get(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await Expanded(db).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null)
        {
            return NotFound(http);
        }

        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(value.ToResponse());
    }
    private static async Task<IResult> Create(ExperienceRequest request, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var errors = InputValidation.Experience(request);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(http, errors);
        }

        if (!await TechnologyIdsAreActive(db, request.TechnologyIds!, ct))
        {
            return TechnologyError(http);
        }

        var value = Build(request);
        db.Experiences.Add(value);
        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Created($"/api/v1/admin/experiences/{value.Id}", value.ToResponse());
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

        var value = await Expanded(db).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null)
        {
            return NotFound(http);
        }

        var precondition = HttpConcurrency.Validate(http, value);
        if (precondition is not null)
        {
            return precondition;
        }

        var patch = new MergePatch(document);
        var request = new ExperienceRequest(patch.Has("company") ? patch.Read<string>("company") : value.Company, patch.Has("role") ? patch.Read<string>("role") : value.Role,
            patch.Has("location") ? patch.Read<string?>("location") : value.Location, patch.Has("startDate") ? patch.Read<DateOnly?>("startDate") : value.StartDate,
            patch.Has("endDate") ? patch.Read<DateOnly?>("endDate") : value.EndDate, patch.Has("summary") ? patch.Read<string>("summary") : value.Summary,
            patch.Has("highlights") ? patch.Read<List<string>>("highlights") : value.Highlights.Select(x => x.Text).ToList(),
            patch.Has("technologyIds") ? patch.Read<List<Guid>>("technologyIds") : value.Technologies.Select(x => x.TechnologyId).ToList());
        var errors = InputValidation.Experience(request);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(http, errors);
        }

        if (!await TechnologyIdsAreActive(db, request.TechnologyIds!, ct))
        {
            return TechnologyError(http);
        }

        value.Company = request.Company!.Trim();
        value.Role = request.Role!.Trim();
        value.Location = request.Location?.Trim();
        value.StartDate = request.StartDate!.Value;
        value.EndDate = request.EndDate;
        value.Summary = request.Summary!.Trim();
        if (patch.Has("highlights"))
        {
            db.ExperienceHighlights.RemoveRange(value.Highlights.ToList());
            db.ExperienceHighlights.AddRange(request.Highlights!.Select(x => new ExperienceHighlight { ExperienceId = value.Id, Text = x.Trim() }));
        }
        if (patch.Has("technologyIds"))
        {
            db.ExperienceTechnologies.RemoveRange(value.Technologies.ToList());
            db.ExperienceTechnologies.AddRange(request.TechnologyIds!.Select(x => new ExperienceTechnology { ExperienceId = value.Id, TechnologyId = x }));
        }
        value.Version++;
        if (patch.Has("company") || patch.Has("role") || patch.Has("startDate") || patch.Has("endDate") || patch.Has("summary"))
        {
            value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(value.ToResponse());
    }
    private static async Task<IResult> Delete(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await db.Experiences.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
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
        var value = await db.Experiences.IgnoreQueryFilters().Include(x => x.Technologies).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null || value.DeletedAt is null)
        {
            return NotFound(http);
        }

        var precondition = HttpConcurrency.Validate(http, value);
        if (precondition is not null)
        {
            return precondition;
        }

        if (!await TechnologyIdsAreActive(db, value.Technologies.Select(x => x.TechnologyId).ToList(), ct))
        {
            return ApiProblems.Create(http, 409, "Experience references a deleted technology");
        }

        value.DeletedAt = null;
        value.Version++;
        value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.NoContent();
    }
    private static Experience Build(ExperienceRequest request)
    {
        return new()
        {
            Company = request.Company!.Trim(),
            Role = request.Role!.Trim(),
            Location = request.Location?.Trim(),
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate,
            Summary = request.Summary!.Trim(),
            Highlights = request.Highlights!.Select(x => new ExperienceHighlight { Text = x.Trim() }).ToList(),
            Technologies = request.TechnologyIds!.Select(x => new ExperienceTechnology { TechnologyId = x }).ToList()
        };
    }

    internal static async Task<bool> TechnologyIdsAreActive(PresentationDbContext db, List<Guid> ids, CancellationToken ct)
    {
        return await db.Technologies.CountAsync(x => ids.Contains(x.Id), ct) == ids.Count;
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
        return ApiProblems.Create(http, 404, "Experience not found");
    }
}
