using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestCaseOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_ExamSessionId",
                table: "Assignments");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "TestCases",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "TestCases");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ExamSessionId",
                table: "Assignments",
                column: "ExamSessionId");
        }
    }
}
