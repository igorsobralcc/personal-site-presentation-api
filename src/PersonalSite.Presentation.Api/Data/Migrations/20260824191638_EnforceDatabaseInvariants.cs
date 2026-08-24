using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalSite.Presentation.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDatabaseInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_technologies_version_positive",
                schema: "presentation",
                table: "technologies",
                sql: "version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skills_version_positive",
                schema: "presentation",
                table: "skills",
                sql: "version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skill_categories_version_positive",
                schema: "presentation",
                table: "skill_categories",
                sql: "version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projects_version_positive",
                schema: "presentation",
                table: "projects",
                sql: "version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_profiles_singleton_key",
                schema: "presentation",
                table: "profiles",
                sql: "singleton_key = 'profile'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_profiles_version_positive",
                schema: "presentation",
                table: "profiles",
                sql: "version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_experiences_version_positive",
                schema: "presentation",
                table: "experiences",
                sql: "version > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_technologies_version_positive",
                schema: "presentation",
                table: "technologies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_skills_version_positive",
                schema: "presentation",
                table: "skills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_skill_categories_version_positive",
                schema: "presentation",
                table: "skill_categories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projects_version_positive",
                schema: "presentation",
                table: "projects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_profiles_singleton_key",
                schema: "presentation",
                table: "profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_profiles_version_positive",
                schema: "presentation",
                table: "profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_experiences_version_positive",
                schema: "presentation",
                table: "experiences");
        }
    }
}
