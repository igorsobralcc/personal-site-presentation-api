using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PersonalSite.Presentation.Api.Data;

public static class DevelopmentSeedData
{
    public static async Task SeedAsync(PresentationDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsRelational()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);

        await using IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        if (await HasManagedContentAsync(db, ct))
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }

        var clock = new SeedClock(DateTimeOffset.UtcNow);
        var technologies = CreateTechnologies(clock);
        var technologyByName = technologies.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var profile = CreateProfile(clock);
        var categories = CreateSkillCategories(clock);
        var experiences = CreateExperiences(clock, technologyByName);
        var projects = CreateProjects(clock, technologyByName);

        db.AddRange(profile);
        db.AddRange(technologies);
        db.AddRange(categories);
        db.AddRange(experiences);
        db.AddRange(projects);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private static async Task<bool> HasManagedContentAsync(PresentationDbContext db, CancellationToken ct) =>
        await db.Profiles.IgnoreQueryFilters().AnyAsync(ct) ||
        await db.Experiences.IgnoreQueryFilters().AnyAsync(ct) ||
        await db.Projects.IgnoreQueryFilters().AnyAsync(ct) ||
        await db.SkillCategories.IgnoreQueryFilters().AnyAsync(ct) ||
        await db.Skills.IgnoreQueryFilters().AnyAsync(ct) ||
        await db.Technologies.IgnoreQueryFilters().AnyAsync(ct);

    private static Profile CreateProfile(SeedClock clock)
    {
        var profile = Stamp(new Profile
        {
            FullName = "Igor Sobral",
            Headline = "Senior Software Engineer | .NET | Distributed Systems | AWS & Azure",
            Biography = "Senior Software Developer with 5+ years of experience designing, modernizing, and delivering high-availability backend systems. Specialized in .NET and C#, backend APIs, microservices, event-driven systems, AWS, and cloud architecture. Experienced in leading technically challenging initiatives, coordinating backend teams, and collaborating with database, product, security, and business teams. Key results include reducing image storage by 60%, removing up to 3 TB of database usage, cutting image publishing time from as much as 36 hours to approximately 5 minutes, reducing product-platform errors by 80%, eliminating more than 50 monthly production incidents, and delivering payment and product-registration platforms with measurable cost savings.",
            ShortSummary = "Senior backend engineer focused on .NET, distributed systems, event-driven architecture, and cloud platforms, combining hands-on delivery with technical leadership.",
            Email = "igorsobral.cc@gmail.com",
            Availability = "Open to senior backend engineering and technical leadership opportunities.",
            CurrentFocus = "Distributed backend systems, cloud architecture, and secure, scalable software.",
        }, clock.Next());
        profile.SocialLinks.Add(new ProfileSocialLink
        {
            ProfileId = profile.Id,
            Label = "LinkedIn",
            Url = "https://www.linkedin.com/in/igor-sobral-m",
            CreatedAt = clock.Next()
        });
        return profile;
    }

    private static List<Technology> CreateTechnologies(SeedClock clock) =>
    [
        Technology(".NET", clock), Technology("C#", clock), Technology("PostgreSQL", clock),
        Technology("React", clock), Technology("RabbitMQ", clock), Technology("MongoDB", clock),
        Technology("ConfigCat", clock), Technology("AWS", clock), Technology("Azure", clock),
        Technology("Azure Key Vault", clock), Technology("Domain-Driven Design", clock),
        Technology("SAP", clock), Technology("Oracle Database", clock), Technology("AWS S3", clock),
        Technology("WebP", clock), Technology("Qlik Replicate", clock), Technology("Active Directory", clock),
        Technology("Microsoft Office", clock), Technology("Windows", clock), Technology("Java", clock),
        Technology("MySQL", clock)
    ];

    private static List<SkillCategory> CreateSkillCategories(SeedClock clock) =>
    [
        Category("Backend Engineering", ["C#", ".NET", "Backend APIs", "Microservices"], clock),
        Category("Architecture & Integration", ["Event-Driven Architecture", "Domain-Driven Design", "RabbitMQ", "Webhooks", "SAP", "Qlik Replicate"], clock),
        Category("Cloud & Delivery", ["AWS", "Azure", "AWS S3", "Azure Key Vault", "CI/CD", "ConfigCat"], clock),
        Category("Data", ["SQL", "PostgreSQL", "Oracle Database", "MongoDB", "MySQL"], clock),
        Category("Frontend & Other Languages", ["React", "Java"], clock)
    ];

    private static List<Experience> CreateExperiences(SeedClock clock, IReadOnlyDictionary<string, Technology> technologies) =>
    [
        Experience("Self-Employed", "Senior Software Engineer Consultant", new(2026, 7, 1), null,
            "Leading the design and delivery of a scalable digital platform for an automotive parts and vehicle-maintenance business using .NET, React, PostgreSQL, microservices, RabbitMQ, and event-driven workflows.",
            [
                "Leading the end-to-end design and development of a digital platform that translates automotive retail and maintenance operations into a scalable full-stack solution.",
                "Designed a distributed .NET microservices architecture with RabbitMQ and event-driven communication.",
                "Built foundations for products, inventory, sales, customers, suppliers, users, permissions, reporting, and operational auditing.",
                "Implemented a CI/CD pipeline for consistent, lower-risk builds and delivery.",
                "Designed advance vehicle-maintenance scheduling to reduce delays during in-person visits.",
                "Partnered directly with the client to define business rules, priorities, requirements, and an incremental delivery roadmap."
            ], [".NET", "React", "PostgreSQL", "RabbitMQ"], technologies, clock),
        Experience("Localiza&Co", "Senior Backend Software Developer", new(2025, 4, 1), new(2026, 7, 31),
            "Delivered high-throughput payment capabilities across four interconnected .NET APIs, including the Getnet integration for Mexican rental operations, while improving test coverage, release safety, and cloud security.",
            [
                "Integrated the Getnet payment provider across four .NET APIs, enabling Mexican car rental operations and reducing payment costs by an estimated R$200K per month.",
                "Engineered asynchronous payment workflows using .NET, RabbitMQ, MongoDB, webhooks, and callbacks.",
                "Raised automated unit, integration, and load-test coverage above 90% across four payment APIs.",
                "Introduced ConfigCat feature flags for phased rollouts, rapid deactivation, and controlled A/B testing.",
                "Coordinated secret-management and dependency migration from Azure Key Vault to AWS.",
                "Adapted business-critical systems for Brazil's new alphanumeric CNPJ format."
            ], [".NET", "RabbitMQ", "MongoDB", "ConfigCat", "AWS", "Azure Key Vault"], technologies, clock),
        Experience("FCx Labs", "Senior Software Engineer / Tech Lead", new(2024, 9, 1), new(2025, 3, 31),
            "Led a five-person backend team in delivering an in-house product registration platform in under two months using .NET, Domain-Driven Design, event-driven architecture, and cloud/on-premises integrations.",
            [
                "Led a five-person backend team and delivered the platform in under two months against an original estimate exceeding three months.",
                "Replaced a subscription product with an in-house platform projected to save R$150K annually.",
                "Integrated SAP, PostgreSQL, and Oracle Database across cloud and on-premises environments.",
                "Implemented near-real-time synchronization that propagated product updates within five seconds.",
                "Designed reversible persisted-data changes to improve operational control.",
                "Increased automated test coverage from 0% to 35% and reduced the error rate by 80%."
            ], [".NET", "Domain-Driven Design", "SAP", "PostgreSQL", "Oracle Database"], technologies, clock),
        Experience("Home Center Ferreira Costa", "Software Engineer", new(2022, 5, 1), new(2024, 9, 30),
            "Architected a centralized .NET and AWS S3 image platform that replaced redundant database BLOB storage, reduced storage by 60%, removed up to 3 TB from databases, and cut publishing time from 36 hours to about 5 minutes.",
            [
                "Delivered a centralized .NET and AWS S3 upload and storage service replacing redundant files across more than 10 PostgreSQL and Oracle tables.",
                "Reduced storage consumption by 60% using WebP, deduplication, and object storage.",
                "Reduced database usage by up to 3 TB and lowered annual infrastructure expense.",
                "Accelerated image publishing from up to 36 hours to approximately 5 minutes using Qlik Replicate across three databases.",
                "Implemented secure backups that reduced lost or corrupted files and production incidents.",
                "Coordinated application and database teams for a solution supporting four high-availability e-commerce platforms."
            ], [".NET", "AWS S3", "PostgreSQL", "Oracle Database", "WebP", "Qlik Replicate"], technologies, clock),
        Experience("Home Center Ferreira Costa", "IT Support Assistant - Apprentice", new(2021, 12, 1), new(2022, 5, 31),
            "Supported retail IT operations through workstation repair, Windows and Microsoft Office configuration, Active Directory access, licensing support, and network infrastructure organization.",
            [
                "Diagnosed hardware and software issues, repaired computers, replaced components, and restored employee workstations.",
                "Configured Windows, Microsoft Office, licenses, and corporate access through Active Directory.",
                "Resolved Windows and Microsoft Office licensing and activation issues.",
                "Labeled and routed network cabling to improve identification and workstation expansion.",
                "Assisted senior technicians with hardware maintenance and end-user support."
            ], ["Windows", "Microsoft Office", "Active Directory"], technologies, clock)
    ];

    private static List<Project> CreateProjects(SeedClock clock, IReadOnlyDictionary<string, Technology> technologies) =>
    [
        Project("E-commerce Image Platform", "A centralized .NET and AWS S3 media platform that reduced storage by 60%, removed up to 3 TB of database usage, and shortened image publishing from 36 hours to about 5 minutes.", [".NET", "AWS S3", "PostgreSQL", "Oracle Database", "WebP", "Qlik Replicate"], technologies, clock),
        Project("Product Registration Platform", "An in-house event-driven product platform delivered by a five-person team in under two months, integrating SAP, PostgreSQL, and Oracle while replacing a subscription service.", [".NET", "Domain-Driven Design", "SAP", "PostgreSQL", "Oracle Database"], technologies, clock),
        Project("Getnet Payment Integration", "A high-throughput asynchronous payment integration spanning four .NET APIs, supporting Mexican rental operations and an estimated R$200K in monthly processing-cost savings.", [".NET", "RabbitMQ", "MongoDB", "AWS"], technologies, clock),
        Project("Automotive Operations Platform", "A scalable full-stack platform centralizing automotive products, inventory, sales, customers, suppliers, permissions, reporting, auditing, and vehicle-maintenance scheduling.", [".NET", "React", "PostgreSQL", "RabbitMQ"], technologies, clock)
    ];

    private static Technology Technology(string name, SeedClock clock) => Stamp(new Technology { Name = name, NormalizedName = name.Trim().ToUpperInvariant() }, clock.Next());

    private static SkillCategory Category(string name, IReadOnlyList<string> skills, SeedClock clock)
    {
        var category = Stamp(new SkillCategory { Name = name, NormalizedName = name.ToUpperInvariant() }, clock.Next());
        foreach (var skillName in skills)
            category.Skills.Add(Stamp(new Skill { Name = skillName, NormalizedName = skillName.ToUpperInvariant(), CategoryId = category.Id }, clock.Next()));
        return category;
    }

    private static Experience Experience(string company, string role, DateOnly startDate, DateOnly? endDate, string summary,
        IReadOnlyList<string> highlights, IReadOnlyList<string> technologyNames, IReadOnlyDictionary<string, Technology> technologies, SeedClock clock)
    {
        var experience = Stamp(new Experience { Company = company, Role = role, StartDate = startDate, EndDate = endDate, Summary = summary }, clock.Next());
        foreach (var highlight in highlights)
            experience.Highlights.Add(new ExperienceHighlight { ExperienceId = experience.Id, Text = highlight, CreatedAt = clock.Next() });
        foreach (var technologyName in technologyNames)
            experience.Technologies.Add(new ExperienceTechnology { ExperienceId = experience.Id, TechnologyId = technologies[technologyName].Id });
        return experience;
    }

    private static Project Project(string name, string summary, IReadOnlyList<string> technologyNames,
        IReadOnlyDictionary<string, Technology> technologies, SeedClock clock)
    {
        var project = Stamp(new Project { Name = name, Summary = summary, IsFeatured = true }, clock.Next());
        foreach (var technologyName in technologyNames)
            project.Technologies.Add(new ProjectTechnology { ProjectId = project.Id, TechnologyId = technologies[technologyName].Id });
        return project;
    }

    private static T Stamp<T>(T entity, DateTimeOffset timestamp) where T : ManagedEntity
    {
        entity.CreatedAt = timestamp;
        entity.UpdatedAt = timestamp;
        entity.PublicUpdatedAt = timestamp;
        return entity;
    }

    private sealed class SeedClock(DateTimeOffset value)
    {
        private DateTimeOffset _value = value;
        public DateTimeOffset Next() => _value = _value.AddTicks(1);
    }

}
