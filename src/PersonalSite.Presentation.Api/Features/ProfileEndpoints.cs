using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalSite.Presentation.Api.Common;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public static class ProfileEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/profile");
        group.MapGet("", Get);
        group.MapPut("", Create);
        group.MapMethods("", ["PATCH"], Patch).Accepts<JsonElement>("application/merge-patch+json");
    }

    private static async Task<IResult> Get(PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var profile = await db.Profiles.Include(x => x.SocialLinks).SingleOrDefaultAsync(ct);
        if (profile is null) return ApiProblems.Create(http, 404, "Profile not found");
        HttpConcurrency.Set(http.Response, profile.Version);
        return Results.Ok(profile.ToResponse());
    }

    private static async Task<IResult> Create(ProfileRequest request, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        var errors = InputValidation.Profile(request);
        if (errors.Count > 0) return ApiProblems.Validation(http, errors);
        if (await db.Profiles.IgnoreQueryFilters().AnyAsync(ct)) return ApiProblems.Create(http, 409, "Profile already exists");
        var profile = new Profile
        {
            FullName = request.FullName!.Trim(), Headline = request.Headline!.Trim(), Biography = request.Biography!.Trim(),
            ShortSummary = Trim(request.ShortSummary), Location = Trim(request.Location), Email = Trim(request.Email),
            Availability = Trim(request.Availability), CurrentFocus = Trim(request.CurrentFocus),
            SocialLinks = request.SocialLinks!.Select(x => new ProfileSocialLink { Label = x.Label!.Trim(), Url = x.Url! }).ToList()
        };
        db.Profiles.Add(profile);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return ApiProblems.Create(http, 409, "Profile already exists"); }
        HttpConcurrency.Set(http.Response, profile.Version);
        return Results.Created("/api/v1/admin/profile", profile.ToResponse());
    }

    private static async Task<IResult> Patch(JsonElement document, PresentationDbContext db, HttpContext http, CancellationToken ct)
    {
        if (document.ValueKind != JsonValueKind.Object) return ApiProblems.Validation(http, new() { ["document"] = ["A JSON object is required."] });
        var profile = await db.Profiles.Include(x => x.SocialLinks).SingleOrDefaultAsync(ct);
        if (profile is null) return ApiProblems.Create(http, 404, "Profile not found");
        var precondition = HttpConcurrency.Validate(http, profile);
        if (precondition is not null) return precondition;
        var patch = new MergePatch(document);
        var request = new ProfileRequest(
            patch.Has("fullName") ? patch.Read<string>("fullName") : profile.FullName,
            patch.Has("headline") ? patch.Read<string>("headline") : profile.Headline,
            patch.Has("biography") ? patch.Read<string>("biography") : profile.Biography,
            patch.Has("shortSummary") ? patch.Read<string?>("shortSummary") : profile.ShortSummary,
            patch.Has("location") ? patch.Read<string?>("location") : profile.Location,
            patch.Has("email") ? patch.Read<string?>("email") : profile.Email,
            patch.Has("availability") ? patch.Read<string?>("availability") : profile.Availability,
            patch.Has("currentFocus") ? patch.Read<string?>("currentFocus") : profile.CurrentFocus,
            patch.Has("socialLinks") ? patch.Read<List<SocialLinkRequest>>("socialLinks") : profile.SocialLinks.Select(x => new SocialLinkRequest(x.Label, x.Url)).ToList());
        var errors = InputValidation.Profile(request);
        if (errors.Count > 0) return ApiProblems.Validation(http, errors);
        profile.FullName = request.FullName!.Trim(); profile.Headline = request.Headline!.Trim(); profile.Biography = request.Biography!.Trim();
        profile.ShortSummary = Trim(request.ShortSummary); profile.Location = Trim(request.Location); profile.Email = Trim(request.Email);
        profile.Availability = Trim(request.Availability); profile.CurrentFocus = Trim(request.CurrentFocus);
        if (patch.Has("socialLinks"))
        {
            db.ProfileSocialLinks.RemoveRange(profile.SocialLinks.ToList());
            db.ProfileSocialLinks.AddRange(request.SocialLinks!.Select(x => new ProfileSocialLink { ProfileId = profile.Id, Label = x.Label!.Trim(), Url = x.Url! }));
        }
        profile.Version++; profile.PublicUpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ApiProblems.Create(http, 412, "Precondition Failed"); }
        HttpConcurrency.Set(http.Response, profile.Version);
        return Results.Ok(profile.ToResponse());
    }

    private static string? Trim(string? value) => value?.Trim();
}
