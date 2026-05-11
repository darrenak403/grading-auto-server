using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(GradingDbContext))]
    [Migration("20260417130000_AssignmentCodeUniquePerSession")]
    public partial class AssignmentCodeUniquePerSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old global unique index on Code
            migrationBuilder.DropIndex(
                name: "IX_Assignments_Code",
                table: "Assignments");

            // Unique within a session (when ExamSessionId is set)
            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ExamSessionId_Code",
                table: "Assignments",
                columns: new[] { "ExamSessionId", "Code" },
                unique: true,
                filter: "\"ExamSessionId\" IS NOT NULL");

            // Unique globally only for session-less assignments
            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Code",
                table: "Assignments",
                column: "Code",
                unique: true,
                filter: "\"ExamSessionId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_ExamSessionId_Code",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_Code",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Code",
                table: "Assignments",
                column: "Code",
                unique: true);
        }
    }
}
