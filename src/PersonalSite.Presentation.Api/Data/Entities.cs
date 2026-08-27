namespace PersonalSite.Presentation.Api.Data;

public abstract class ManagedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset PublicUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt
    {
        get; set;
    }
    public long Version { get; set; } = 1;
}

public sealed class Profile : ManagedEntity
{
    public const string Singleton = "profile";
    public string SingletonKey { get; set; } = Singleton;
    public required string FullName
    {
        get; set;
    }
    public required string Headline
    {
        get; set;
    }
    public required string Biography
    {
        get; set;
    }
    public string? ShortSummary
    {
        get; set;
    }
    public string? Location
    {
        get; set;
    }
    public string? Email
    {
        get; set;
    }
    public string? Availability
    {
        get; set;
    }
    public string? CurrentFocus
    {
        get; set;
    }
    public List<ProfileSocialLink> SocialLinks { get; set; } = [];
}

public sealed class ProfileSocialLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProfileId
    {
        get; set;
    }
    public required string Label
    {
        get; set;
    }
    public required string Url
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Experience : ManagedEntity
{
    public required string Company
    {
        get; set;
    }
    public required string Role
    {
        get; set;
    }
    public string? Location
    {
        get; set;
    }
    public DateOnly StartDate
    {
        get; set;
    }
    public DateOnly? EndDate
    {
        get; set;
    }
    public required string Summary
    {
        get; set;
    }
    public List<ExperienceHighlight> Highlights { get; set; } = [];
    public List<ExperienceTechnology> Technologies { get; set; } = [];
}

public sealed class ExperienceHighlight
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ExperienceId
    {
        get; set;
    }
    public required string Text
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Project : ManagedEntity
{
    public required string Name
    {
        get; set;
    }
    public required string Summary
    {
        get; set;
    }
    public string? RepositoryUrl
    {
        get; set;
    }
    public string? LiveUrl
    {
        get; set;
    }
    public bool IsFeatured
    {
        get; set;
    }
    public string? ImageUrl
    {
        get; set;
    }
    public string? ImageAlt
    {
        get; set;
    }
    public int? ImageWidth
    {
        get; set;
    }
    public int? ImageHeight
    {
        get; set;
    }
    public List<ProjectTechnology> Technologies { get; set; } = [];
}

public sealed class SkillCategory : ManagedEntity
{
    public required string Name
    {
        get; set;
    }
    public required string NormalizedName
    {
        get; set;
    }
    public List<Skill> Skills { get; set; } = [];
}

public sealed class Skill : ManagedEntity
{
    public required string Name
    {
        get; set;
    }
    public required string NormalizedName
    {
        get; set;
    }
    public Guid CategoryId
    {
        get; set;
    }
    public SkillCategory Category { get; set; } = null!;
}

public sealed class Technology : ManagedEntity
{
    public required string Name
    {
        get; set;
    }
    public required string NormalizedName
    {
        get; set;
    }
}

public sealed class ExperienceTechnology
{
    public Guid ExperienceId
    {
        get; set;
    }
    public Experience Experience { get; set; } = null!;
    public Guid TechnologyId
    {
        get; set;
    }
    public Technology Technology { get; set; } = null!;
}

public sealed class ProjectTechnology
{
    public Guid ProjectId
    {
        get; set;
    }
    public Project Project { get; set; } = null!;
    public Guid TechnologyId
    {
        get; set;
    }
    public Technology Technology { get; set; } = null!;
}
