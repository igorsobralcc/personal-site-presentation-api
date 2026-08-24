using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public static class NamedResourceEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        MapCategories(admin.MapGroup("/skill-categories"));
        MapTechnologies(admin.MapGroup("/technologies"));
    }

    private static void MapCategories(RouteGroupBuilder group)
    {
        group.MapGet("", ListCategories); group.MapPost("", CreateCategory);
        group.MapGet("/{id:guid}", GetCategory); group.MapMethods("/{id:guid}", ["PATCH"], PatchCategory).Accepts<JsonElement>("application/merge-patch+json");
        group.MapDelete("/{id:guid}", DeleteCategory); group.MapPost("/{id:guid}/restore", RestoreCategory);
    }
    private static void MapTechnologies(RouteGroupBuilder group)
    {
        group.MapGet("", ListTechnologies); group.MapPost("", CreateTechnology);
        group.MapGet("/{id:guid}", GetTechnology); group.MapMethods("/{id:guid}", ["PATCH"], PatchTechnology).Accepts<JsonElement>("application/merge-patch+json");
        group.MapDelete("/{id:guid}", DeleteTechnology); group.MapPost("/{id:guid}/restore", RestoreTechnology);
    }

    private static async Task<IResult> ListCategories(PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!PageRequest.TryRead(http.Request, out var page, out var errors)) return ApiProblems.Validation(http, errors);
        var query = page.IncludeDeleted ? db.SkillCategories.IgnoreQueryFilters() : db.SkillCategories;
        var count = await query.CountAsync(ct);
        var entities = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<NamedResponse>(entities.Select(x => x.ToResponse()).ToList(), page.Page, page.PageSize, count, (int)Math.Ceiling(count / (double)page.PageSize)));
    }
    private static async Task<IResult> ListTechnologies(PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!PageRequest.TryRead(http.Request, out var page, out var errors)) return ApiProblems.Validation(http, errors);
        var query = page.IncludeDeleted ? db.Technologies.IgnoreQueryFilters() : db.Technologies;
        var count = await query.CountAsync(ct);
        var entities = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<NamedResponse>(entities.Select(x => x.ToResponse()).ToList(), page.Page, page.PageSize, count, (int)Math.Ceiling(count / (double)page.PageSize)));
    }
    private static Task<IResult> CreateCategory(NamedRequest request, PresentationDbContext db, HttpContext http, CancellationToken ct) => Create(request, db, http, true, ct);
    private static Task<IResult> CreateTechnology(NamedRequest request, PresentationDbContext db, HttpContext http, CancellationToken ct) => Create(request, db, http, false, ct);
    private static async Task<IResult> Create(NamedRequest request, PresentationDbContext db, HttpContext http, bool category, CancellationToken ct)
    {
        var errors = InputValidation.Named(request.Name); if (errors.Count > 0) return ApiProblems.Validation(http, errors);
        var normalized = InputValidation.Normalize(request.Name!);
        if (category)
        {
            if (await db.SkillCategories.AnyAsync(x => x.NormalizedName == normalized, ct)) return Conflict(http);
            var value = new SkillCategory { Name = request.Name!.Trim(), NormalizedName = normalized }; db.SkillCategories.Add(value); await db.SaveChangesAsync(ct);
            HttpConcurrency.Set(http.Response, value.Version); return Results.Created($"/api/v1/admin/skill-categories/{value.Id}", value.ToResponse());
        }
        if (await db.Technologies.AnyAsync(x => x.NormalizedName == normalized, ct)) return Conflict(http);
        var technology = new Technology { Name = request.Name!.Trim(), NormalizedName = normalized }; db.Technologies.Add(technology); await db.SaveChangesAsync(ct);
        HttpConcurrency.Set(http.Response, technology.Version); return Results.Created($"/api/v1/admin/technologies/{technology.Id}", technology.ToResponse());
    }
    private static Task<IResult> GetCategory(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct) => Get(id, db, http, true, ct);
    private static Task<IResult> GetTechnology(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct) => Get(id, db, http, false, ct);
    private static async Task<IResult> Get(Guid id, PresentationDbContext db, HttpContext http, bool category, CancellationToken ct)
    {
        ManagedEntity? value = category ? await db.SkillCategories.FindAsync([id], ct) : await db.Technologies.FindAsync([id], ct);
        if (value is null) return NotFound(http); HttpConcurrency.Set(http.Response, value.Version);
        return Results.Ok(category ? ((SkillCategory)value).ToResponse() : ((Technology)value).ToResponse());
    }
    private static Task<IResult> PatchCategory(Guid id, JsonElement document, PresentationDbContext db, HttpContext http, CancellationToken ct) => Patch(id, document, db, http, true, ct);
    private static Task<IResult> PatchTechnology(Guid id, JsonElement document, PresentationDbContext db, HttpContext http, CancellationToken ct) => Patch(id, document, db, http, false, ct);
    private static async Task<IResult> Patch(Guid id, JsonElement document, PresentationDbContext db, HttpContext http, bool category, CancellationToken ct)
    {
        if (document.ValueKind != JsonValueKind.Object) return ApiProblems.Validation(http, new() { ["document"] = ["A JSON object is required."] });
        ManagedEntity? entity = category ? await db.SkillCategories.FindAsync([id], ct) : await db.Technologies.FindAsync([id], ct);
        if (entity is null) return NotFound(http); var precondition = HttpConcurrency.Validate(http, entity); if (precondition is not null) return precondition;
        var patch = new MergePatch(document); var current = category ? ((SkillCategory)entity).Name : ((Technology)entity).Name;
        var name = patch.Has("name") ? patch.Read<string>("name") : current; var errors = InputValidation.Named(name); if (errors.Count > 0) return ApiProblems.Validation(http, errors);
        var normalized = InputValidation.Normalize(name!);
        var duplicate = category ? await db.SkillCategories.AnyAsync(x => x.Id != id && x.NormalizedName == normalized, ct) : await db.Technologies.AnyAsync(x => x.Id != id && x.NormalizedName == normalized, ct);
        if (duplicate) return Conflict(http);
        if (category) { ((SkillCategory)entity).Name = name!.Trim(); ((SkillCategory)entity).NormalizedName = normalized; }
        else { ((Technology)entity).Name = name!.Trim(); ((Technology)entity).NormalizedName = normalized; }
        entity.Version++; entity.PublicUpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); HttpConcurrency.Set(http.Response, entity.Version);
        return Results.Ok(category ? ((SkillCategory)entity).ToResponse() : ((Technology)entity).ToResponse());
    }
    private static Task<IResult> DeleteCategory(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct) => Delete(id, db, http, true, ct);
    private static Task<IResult> DeleteTechnology(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct) => Delete(id, db, http, false, ct);
    private static async Task<IResult> Delete(Guid id, PresentationDbContext db, HttpContext http, bool category, CancellationToken ct)
    {
        var entity = category ? (ManagedEntity?)await db.SkillCategories.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct) : await db.Technologies.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound(http);
        if (!http.Request.Headers.ContainsKey("If-Match")) return ApiProblems.Create(http, 428, "Precondition Required", "If-Match is required.");
        if (entity.DeletedAt is not null) return Results.NoContent();
        var precondition = HttpConcurrency.Validate(http, entity); if (precondition is not null) return precondition;
        var referenced = category ? await db.Skills.AnyAsync(x => x.CategoryId == id, ct) : await db.ExperienceTechnologies.AnyAsync(x => x.TechnologyId == id && x.Experience.DeletedAt == null, ct) || await db.ProjectTechnologies.AnyAsync(x => x.TechnologyId == id && x.Project.DeletedAt == null, ct);
        if (referenced) return ApiProblems.Create(http, 409, "Resource is in use");
        entity.DeletedAt = DateTimeOffset.UtcNow; entity.Version++; entity.PublicUpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); HttpConcurrency.Set(http.Response, entity.Version); return Results.NoContent();
    }
    private static Task<IResult> RestoreCategory(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct) => Restore(id, db, http, true, ct);
    private static Task<IResult> RestoreTechnology(Guid id, PresentationDbContext db, HttpContext http, CancellationToken ct) => Restore(id, db, http, false, ct);
    private static async Task<IResult> Restore(Guid id, PresentationDbContext db, HttpContext http, bool category, CancellationToken ct)
    {
        var entity = category ? (ManagedEntity?)await db.SkillCategories.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct) : await db.Technologies.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || entity.DeletedAt is null) return NotFound(http); var precondition = HttpConcurrency.Validate(http, entity); if (precondition is not null) return precondition;
        var normalized = category ? ((SkillCategory)entity).NormalizedName : ((Technology)entity).NormalizedName;
        var duplicate = category ? await db.SkillCategories.AnyAsync(x => x.NormalizedName == normalized, ct) : await db.Technologies.AnyAsync(x => x.NormalizedName == normalized, ct);
        if (duplicate) return Conflict(http); entity.DeletedAt = null; entity.Version++; entity.PublicUpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); HttpConcurrency.Set(http.Response, entity.Version); return Results.NoContent();
    }
    private static IResult Conflict(HttpContext http) => ApiProblems.Create(http, 409, "Name already exists");
    private static IResult NotFound(HttpContext http) => ApiProblems.Create(http, 404, "Resource not found");
}
