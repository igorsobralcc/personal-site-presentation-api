using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalSite.Presentation.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPresentationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "presentation");

            migrationBuilder.CreateTable(
                name: "experiences",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    role = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experiences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    singleton_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    biography = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    short_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    availability = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    current_focus = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    repository_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    live_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    image_alt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_width = table.Column<int>(type: "integer", nullable: true),
                    image_height = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_categories",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "technologies",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technologies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "experience_highlights",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experience_highlights", x => x.id);
                    table.ForeignKey(
                        name: "fk_experience_highlights_experiences_experience_id",
                        column: x => x.experience_id,
                        principalSchema: "presentation",
                        principalTable: "experiences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_social_links",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_social_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_profile_social_links_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "presentation",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "presentation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_skills_skill_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "presentation",
                        principalTable: "skill_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "experience_technologies",
                schema: "presentation",
                columns: table => new
                {
                    experience_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experience_technologies", x => new { x.experience_id, x.technology_id });
                    table.ForeignKey(
                        name: "fk_experience_technologies_experiences_experience_id",
                        column: x => x.experience_id,
                        principalSchema: "presentation",
                        principalTable: "experiences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_experience_technologies_technologies_technology_id",
                        column: x => x.technology_id,
                        principalSchema: "presentation",
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_technologies",
                schema: "presentation",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_technologies", x => new { x.project_id, x.technology_id });
                    table.ForeignKey(
                        name: "fk_project_technologies_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "presentation",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_technologies_technologies_technology_id",
                        column: x => x.technology_id,
                        principalSchema: "presentation",
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_experience_highlights_experience_id",
                schema: "presentation",
                table: "experience_highlights",
                column: "experience_id");

            migrationBuilder.CreateIndex(
                name: "ix_experience_technologies_technology_id",
                schema: "presentation",
                table: "experience_technologies",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "ix_profile_social_links_profile_id",
                schema: "presentation",
                table: "profile_social_links",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_profiles_singleton_key",
                schema: "presentation",
                table: "profiles",
                column: "singleton_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_technologies_technology_id",
                schema: "presentation",
                table: "project_technologies",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_categories_normalized_name",
                schema: "presentation",
                table: "skill_categories",
                column: "normalized_name",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_skills_category_id_normalized_name",
                schema: "presentation",
                table: "skills",
                columns: new[] { "category_id", "normalized_name" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_technologies_normalized_name",
                schema: "presentation",
                table: "technologies",
                column: "normalized_name",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experience_highlights",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "experience_technologies",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "profile_social_links",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "project_technologies",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "experiences",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "profiles",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "technologies",
                schema: "presentation");

            migrationBuilder.DropTable(
                name: "skill_categories",
                schema: "presentation");
        }
    }
}
