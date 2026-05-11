using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentConfigAndQuestionFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GivenApiZipPath",
                table: "Assignments");

            migrationBuilder.AddColumn<string>(
                name: "CollectionJsonPath",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactFolderName",
                table: "Questions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GivenApiBaseUrl",
                table: "Assignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtifactFolderName",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "GivenApiBaseUrl",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "CollectionJsonPath",
                table: "Assignments");

            migrationBuilder.AddColumn<string>(
                name: "GivenApiZipPath",
                table: "Assignments",
                type: "text",
                nullable: true);
        }
    }
}
