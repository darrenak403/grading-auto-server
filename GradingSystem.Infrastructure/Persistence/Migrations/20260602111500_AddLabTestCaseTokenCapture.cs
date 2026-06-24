using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GradingDbContext))]
    [Migration("20260602111500_AddLabTestCaseTokenCapture")]
    public partial class AddLabTestCaseTokenCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SaveTokenFrom",
                table: "LabTestCases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaveTokenFrom",
                table: "LabTestCases");
        }
    }
}
