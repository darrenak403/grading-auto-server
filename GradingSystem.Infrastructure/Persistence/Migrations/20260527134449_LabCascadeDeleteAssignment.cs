using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabCascadeDeleteAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabSubmissions_LabAssignments_LabAssignmentId",
                table: "LabSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_LabTestCaseResults_LabTestCases_LabTestCaseId",
                table: "LabTestCaseResults");

            migrationBuilder.AddForeignKey(
                name: "FK_LabSubmissions_LabAssignments_LabAssignmentId",
                table: "LabSubmissions",
                column: "LabAssignmentId",
                principalTable: "LabAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestCaseResults_LabTestCases_LabTestCaseId",
                table: "LabTestCaseResults",
                column: "LabTestCaseId",
                principalTable: "LabTestCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabSubmissions_LabAssignments_LabAssignmentId",
                table: "LabSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_LabTestCaseResults_LabTestCases_LabTestCaseId",
                table: "LabTestCaseResults");

            migrationBuilder.AddForeignKey(
                name: "FK_LabSubmissions_LabAssignments_LabAssignmentId",
                table: "LabSubmissions",
                column: "LabAssignmentId",
                principalTable: "LabAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestCaseResults_LabTestCases_LabTestCaseId",
                table: "LabTestCaseResults",
                column: "LabTestCaseId",
                principalTable: "LabTestCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
