using Microsoft.EntityFrameworkCore;

namespace PersonalSite.Presentation.Api.Data;

public sealed class PresentationDbContext(DbContextOptions<PresentationDbContext> options) : DbContext(options)
{
    public const string Schema = "presentation";
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfileSocialLink> ProfileSocialLinks => Set<ProfileSocialLink>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<ExperienceHighlight> ExperienceHighlights => Set<ExperienceHighlight>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Technology> Technologies => Set<Technology>();
    public DbSet<ExperienceTechnology> ExperienceTechnologies => Set<ExperienceTechnology>();
    public DbSet<ProjectTechnology> ProjectTechnologies => Set<ProjectTechnology>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureManaged<Profile>(modelBuilder, "profiles");
        ConfigureManaged<Experience>(modelBuilder, "experiences");
        ConfigureManaged<Project>(modelBuilder, "projects");
        ConfigureManaged<SkillCategory>(modelBuilder, "skill_categories");
        ConfigureManaged<Skill>(modelBuilder, "skills");
        ConfigureManaged<Technology>(modelBuilder, "technologies");

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasIndex(x => x.SingletonKey).IsUnique();
            entity.Property(x => x.SingletonKey).HasMaxLength(20);
            entity.Property(x => x.FullName).HasMaxLength(120);
            entity.Property(x => x.Headline).HasMaxLength(160);
            entity.Property(x => x.Biography).HasMaxLength(4000);
            entity.Property(x => x.ShortSummary).HasMaxLength(500);
            entity.Property(x => x.Location).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Availability).HasMaxLength(240);
            entity.Property(x => x.CurrentFocus).HasMaxLength(500);
            entity.HasMany(x => x.SocialLinks).WithOne().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProfileSocialLink>(entity =>
        {
            entity.ToTable("profile_social_links");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(40);
            entity.Property(x => x.Url).HasMaxLength(2048);
        });
        modelBuilder.Entity<Experience>(entity =>
        {
            entity.Property(x => x.Company).HasMaxLength(160);
            entity.Property(x => x.Role).HasMaxLength(160);
            entity.Property(x => x.Location).HasMaxLength(160);
            entity.Property(x => x.Summary).HasMaxLength(4000);
            entity.HasMany(x => x.Highlights).WithOne().HasForeignKey(x => x.ExperienceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Technologies).WithOne(x => x.Experience).HasForeignKey(x => x.ExperienceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ExperienceHighlight>(entity =>
        {
            entity.ToTable("experience_highlights");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(500);
        });
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Summary).HasMaxLength(1000);
            entity.Property(x => x.RepositoryUrl).HasMaxLength(2048);
            entity.Property(x => x.LiveUrl).HasMaxLength(2048);
            entity.Property(x => x.ImageUrl).HasMaxLength(2048);
            entity.Property(x => x.ImageAlt).HasMaxLength(500);
            entity.HasMany(x => x.Technologies).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SkillCategory>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.NormalizedName).HasMaxLength(80);
            entity.HasIndex(x => x.NormalizedName).IsUnique().HasFilter("deleted_at IS NULL");
            entity.HasMany(x => x.Skills).WithOne(x => x.Category).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Skill>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.NormalizedName).HasMaxLength(80);
            entity.HasIndex(x => new { x.CategoryId, x.NormalizedName }).IsUnique().HasFilter("deleted_at IS NULL");
        });
        modelBuilder.Entity<Technology>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.NormalizedName).HasMaxLength(80);
            entity.HasIndex(x => x.NormalizedName).IsUnique().HasFilter("deleted_at IS NULL");
        });
        modelBuilder.Entity<ExperienceTechnology>(entity =>
        {
            entity.ToTable("experience_technologies");
            entity.HasKey(x => new { x.ExperienceId, x.TechnologyId });
            entity.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => x.Experience.DeletedAt == null);
        });
        modelBuilder.Entity<ProjectTechnology>(entity =>
        {
            entity.ToTable("project_technologies");
            entity.HasKey(x => new { x.ProjectId, x.TechnologyId });
            entity.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => x.Project.DeletedAt == null);
        });
    }

    private static void ConfigureManaged<TEntity>(ModelBuilder modelBuilder, string table) where TEntity : ManagedEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.ToTable(table);
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => x.DeletedAt == null);
            entity.Property(x => x.Version).IsConcurrencyToken();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ManagedEntity>().Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        return base.SaveChangesAsync(cancellationToken);
    }
}
