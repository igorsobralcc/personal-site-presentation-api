using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public static class SkillEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/skills");
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

        var query = page.IncludeDeleted ? db.Skills.IgnoreQueryFilters() : db.Skills;
        var count = await query.CountAsync(ct);
        var entities = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<SkillResponse>(entities.Select(x => x.ToResponse()).ToList(), page.Page, page.PageSize, count, (int)Math.Ceiling(count / (double)page.PageSize)));
    }
    private static async Task<IResult> Get(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await db.Skills.FindAsync([id], ct);
        if (value is null)
        {
            return NotFound(http);
        }

        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(value.ToResponse());
    }
    private static async Task<IResult> Create(SkillRequest request, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var errors = InputValidation.Skill(request);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(http, errors);
        }

        if (!await db.SkillCategories.AnyAsync(x => x.Id == request.CategoryId, ct))
        {
            return ApiProblems.Validation(http, new()
            {
                ["categoryId"] = ["Must reference an active category."]
            });
        }

        var normalized = InputValidation.Normalize(request.Name!);
        if (await db.Skills.AnyAsync(x => x.CategoryId == request.CategoryId && x.NormalizedName == normalized, ct))
        {
            return Conflict(http);
        }

        var value = new Skill { Name = request.Name!.Trim(), NormalizedName = normalized, CategoryId = request.CategoryId!.Value };
        db.Skills.Add(value);
        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Created($"/api/v1/admin/skills/{value.Id}", value.ToResponse());
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

        var value = await db.Skills.FindAsync([id], ct);
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
        var request = new SkillRequest(patch.Has("name") ? patch.Read<string>("name") : value.Name,
            patch.Has("categoryId") ? patch.Read<Guid?>("categoryId") : value.CategoryId);
        var errors = InputValidation.Skill(request);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(http, errors);
        }

        if (!await db.SkillCategories.AnyAsync(x => x.Id == request.CategoryId, ct))
        {
            return ApiProblems.Validation(http, new()
            {
                ["categoryId"] = ["Must reference an active category."]
            });
        }

        var normalized = InputValidation.Normalize(request.Name!);
        if (await db.Skills.AnyAsync(x => x.Id != id && x.CategoryId == request.CategoryId && x.NormalizedName == normalized, ct))
        {
            return Conflict(http);
        }

        value.Name = request.Name!.Trim();
        value.NormalizedName = normalized;
        value.CategoryId = request.CategoryId!.Value;
        value.Version++;
        value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(value.ToResponse());
    }
    private static async Task<IResult> Delete(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var value = await db.Skills.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
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
        var value = await db.Skills.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null || value.DeletedAt is null)
        {
            return NotFound(http);
        }

        var precondition = HttpConcurrency.Validate(http, value);
        if (precondition is not null)
        {
            return precondition;
        }

        if (!await db.SkillCategories.AnyAsync(x => x.Id == value.CategoryId, ct) || await db.Skills.AnyAsync(x => x.CategoryId == value.CategoryId && x.NormalizedName == value.NormalizedName, ct))
        {
            return ApiProblems.Create(http, 409, "Skill cannot be restored");
        }

        value.DeletedAt = null;
        value.Version++;
        value.PublicUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, value.Version);
        return Results.NoContent();
    }
    private static IResult Conflict(HttpContext http)
    {
        return ApiProblems.Create(http, 409, "Skill already exists in this category");
    }

    private static IResult NotFound(HttpContext http)
    {
        return ApiProblems.Create(http, 404, "Skill not found");
    }
}
