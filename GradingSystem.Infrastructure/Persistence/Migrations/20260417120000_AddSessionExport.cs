using System;
using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(GradingDbContext))]
    [Migration("20260417120000_AddSessionExport")]
    public partial class AddSessionExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old required FK on AssignmentId
            migrationBuilder.DropForeignKey(
                name: "FK_ExportJobs_Assignments_AssignmentId",
                table: "ExportJobs");

            // Make AssignmentId nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "AssignmentId",
                table: "ExportJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Add ExamSessionId column
            migrationBuilder.AddColumn<Guid>(
                name: "ExamSessionId",
                table: "ExportJobs",
                type: "uuid",
                nullable: true);

            // Index for ExamSessionId
            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_ExamSessionId",
                table: "ExportJobs",
                column: "ExamSessionId");

            // Re-add AssignmentId FK as optional
            migrationBuilder.AddForeignKey(
                name: "FK_ExportJobs_Assignments_AssignmentId",
                table: "ExportJobs",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id");

            // Add ExamSessionId FK
            migrationBuilder.AddForeignKey(
                name: "FK_ExportJobs_ExamSessions_ExamSessionId",
                table: "ExportJobs",
                column: "ExamSessionId",
                principalTable: "ExamSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExportJobs_ExamSessions_ExamSessionId",
                table: "ExportJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ExportJobs_Assignments_AssignmentId",
                table: "ExportJobs");

            migrationBuilder.DropIndex(
                name: "IX_ExportJobs_ExamSessionId",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "ExamSessionId",
                table: "ExportJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignmentId",
                table: "ExportJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExportJobs_Assignments_AssignmentId",
                table: "ExportJobs",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
