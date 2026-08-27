using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PersonalSite.Presentation.Api.Data;

namespace PersonalSite.Presentation.Api.Features;

public sealed record SocialLinkRequest(string? Label, string? Url);
public sealed record ProfileRequest(string? FullName, string? Headline, string? Biography, string? ShortSummary,
    string? Location, string? Email, string? Availability, string? CurrentFocus, List<SocialLinkRequest>? SocialLinks);
public sealed record NamedRequest(string? Name);
public sealed record SkillRequest(string? Name, Guid? CategoryId);
public sealed record ExperienceRequest(string? Company, string? Role, string? Location, DateOnly? StartDate,
    DateOnly? EndDate, string? Summary, List<string>? Highlights, List<Guid>? TechnologyIds);
public sealed record ProjectImageRequest(string? Url, string? Alt, int? Width, int? Height);
public sealed record ProjectRequest(string? Name, string? Summary, string? RepositoryUrl, string? LiveUrl,
    List<Guid>? TechnologyIds, bool? IsFeatured, ProjectImageRequest? Image);

public sealed record ResourceMetadata(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, bool IsDeleted);
public sealed record SocialLinkResponse(string Label, string Url);
public sealed record ProfileResponse(Guid Id, string FullName, string Headline, string Biography, string? ShortSummary,
    string? Location, string? Email, string? Availability, string? CurrentFocus,
    IReadOnlyList<SocialLinkResponse> SocialLinks, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, bool IsDeleted);
public sealed record NamedResponse(string Name, Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, bool IsDeleted);
public sealed record SkillResponse(string Name, Guid CategoryId, Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, bool IsDeleted);
public sealed record ExperienceResponse(string Company, string Role, string? Location, DateOnly StartDate, DateOnly? EndDate,
    string Summary, IReadOnlyList<string> Highlights, IReadOnlyList<Guid> TechnologyIds,
    Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, bool IsDeleted);
public sealed record ProjectImageResponse(string Url, string Alt, int Width, int Height);
public sealed record ProjectResponse(string Name, string Summary, string? RepositoryUrl, string? LiveUrl,
    IReadOnlyList<Guid> TechnologyIds, bool IsFeatured, ProjectImageResponse? Image,
    Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, bool IsDeleted);

public static class ContractMappings
{
    public static ProfileResponse ToResponse(this Profile value)
    {
        return new(value.Id, value.FullName, value.Headline,
        value.Biography, value.ShortSummary, value.Location, value.Email, value.Availability, value.CurrentFocus,
        value.SocialLinks.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new SocialLinkResponse(x.Label, x.Url)).ToList(),
        value.CreatedAt, value.UpdatedAt, value.Version, value.DeletedAt is not null);
    }

    public static NamedResponse ToResponse(this SkillCategory value)
    {
        return new(value.Name, value.Id, value.CreatedAt, value.UpdatedAt, value.Version, value.DeletedAt is not null);
    }

    public static NamedResponse ToResponse(this Technology value)
    {
        return new(value.Name, value.Id, value.CreatedAt, value.UpdatedAt, value.Version, value.DeletedAt is not null);
    }

    public static SkillResponse ToResponse(this Skill value)
    {
        return new(value.Name, value.CategoryId, value.Id, value.CreatedAt, value.UpdatedAt, value.Version, value.DeletedAt is not null);
    }

    public static ExperienceResponse ToResponse(this Experience value)
    {
        return new(value.Company, value.Role, value.Location, value.StartDate, value.EndDate,
            value.Summary, value.Highlights.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Text).ToList(),
            value.Technologies.Select(x => x.TechnologyId).Order().ToList(), value.Id, value.CreatedAt, value.UpdatedAt, value.Version, value.DeletedAt is not null);
    }

    public static ProjectResponse ToResponse(this Project value)
    {
        return new(value.Name, value.Summary, value.RepositoryUrl, value.LiveUrl,
            value.Technologies.Select(x => x.TechnologyId).Order().ToList(), value.IsFeatured,
            value.ImageUrl is null ? null : new(value.ImageUrl, value.ImageAlt!, value.ImageWidth!.Value, value.ImageHeight!.Value),
            value.Id, value.CreatedAt, value.UpdatedAt, value.Version, value.DeletedAt is not null);
    }
}

public static class InputValidation
{
    public static Dictionary<string, string[]> Profile(ProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        Required(request.FullName, "fullName", 120, errors);
        Required(request.Headline, "headline", 160, errors);
        Required(request.Biography, "biography", 4000, errors);
        Optional(request.ShortSummary, "shortSummary", 500, errors);
        Optional(request.Location, "location", 160, errors);
        Optional(request.Availability, "availability", 240, errors);
        Optional(request.CurrentFocus, "currentFocus", 500, errors);
        if (request.Email is { } email && (!new EmailAddressAttribute().IsValid(email) || email.Length > 320))
        {
            errors["email"] = ["Must be a valid email address no longer than 320 characters."];
        }

        if (request.SocialLinks is null)
        {
            errors["socialLinks"] = ["Is required."];
        }
        else
        {
            if (request.SocialLinks.Count > 20)
            {
                errors["socialLinks"] = ["Must contain at most 20 items."];
            }

            if (request.SocialLinks.Any(x => string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 40 || !IsAbsoluteHttp(x.Url)))
            {
                errors["socialLinks"] = ["Each link requires a label up to 40 characters and an absolute HTTP or HTTPS URL."];
            }

            if (request.SocialLinks.Where(x => x.Label is not null).GroupBy(x => x.Label!.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            {
                errors["socialLinks"] = ["Link labels must be unique."];
            }
        }
        return errors;
    }

    public static Dictionary<string, string[]> Named(string? name)
    {
        var errors = new Dictionary<string, string[]>();
        Required(name, "name", 80, errors);
        return errors;
    }

    public static Dictionary<string, string[]> Skill(SkillRequest request)
    {
        var errors = Named(request.Name);
        if (request.CategoryId is null || request.CategoryId == Guid.Empty)
        {
            errors["categoryId"] = ["A valid categoryId is required."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Experience(ExperienceRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        Required(request.Company, "company", 160, errors);
        Required(request.Role, "role", 160, errors);
        Optional(request.Location, "location", 160, errors);
        Required(request.Summary, "summary", 4000, errors);
        if (request.StartDate is null)
        {
            errors["startDate"] = ["Is required."];
        }

        if (request.StartDate is not null && request.EndDate < request.StartDate)
        {
            errors["endDate"] = ["Must be on or after startDate."];
        }

        if (request.Highlights is null)
        {
            errors["highlights"] = ["Is required."];
        }
        else if (request.Highlights.Count > 20 || request.Highlights.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500) || request.Highlights.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Highlights.Count)
        {
            errors["highlights"] = ["Must contain at most 20 unique, non-empty values up to 500 characters."];
        }

        ValidateIds(request.TechnologyIds, "technologyIds", 40, errors);
        return errors;
    }

    public static Dictionary<string, string[]> Project(ProjectRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        Required(request.Name, "name", 160, errors);
        Required(request.Summary, "summary", 1000, errors);
        if (request.RepositoryUrl is not null && !IsAbsoluteHttps(request.RepositoryUrl))
        {
            errors["repositoryUrl"] = ["Must be an absolute HTTPS URL."];
        }

        if (request.LiveUrl is not null && !IsAbsoluteHttps(request.LiveUrl))
        {
            errors["liveUrl"] = ["Must be an absolute HTTPS URL."];
        }

        ValidateIds(request.TechnologyIds, "technologyIds", 40, errors);
        if (request.IsFeatured is null)
        {
            errors["isFeatured"] = ["Is required."];
        }

        if (request.Image is { } image && (!IsAbsoluteHttps(image.Url) || string.IsNullOrWhiteSpace(image.Alt) || image.Alt.Length > 500 || image.Width <= 0 || image.Height <= 0))
        {
            errors["image"] = ["Requires an HTTPS URL, alternative text, and positive width and height."];
        }

        return errors;
    }

    public static bool IsAbsoluteHttps(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    public static bool IsAbsoluteHttp(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static void Required(string? value, string field, int max, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
        {
            errors[field] = [$"Is required and must be no longer than {max} characters."];
        }
    }
    private static void Optional(string? value, string field, int max, Dictionary<string, string[]> errors)
    {
        if (value?.Length > max)
        {
            errors[field] = [$"Must be no longer than {max} characters."];
        }
    }
    private static void ValidateIds(List<Guid>? ids, string field, int max, Dictionary<string, string[]> errors)
    {
        if (ids is null)
        {
            errors[field] = ["Is required."];
        }
        else if (ids.Count > max || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
        {
            errors[field] = [$"Must contain at most {max} unique, valid identifiers."];
        }
    }
}

public sealed class MergePatch(JsonElement root)
{
    public bool Has(string name)
    {
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out _);
    }

    public T? Read<T>(string name)
    {
        return root.TryGetProperty(name, out var value) ? value.Deserialize<T>(JsonOptions) : default;
    }

    public bool IsNull(string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
