using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GradingDbContext))]
    [Migration("20260602103000_AddLabTestCaseHeaders")]
    public partial class AddLabTestCaseHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeadersJson",
                table: "LabTestCases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeadersJson",
                table: "LabTestCases");
        }
    }
}
