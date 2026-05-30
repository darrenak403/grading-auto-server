using System;
using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GradingDbContext))]
[Migration("20260529180000_AddLabExportJob")]
public partial class AddLabExportJob : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "LabAssignmentId",
            table: "ExportJobs",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExportJobs_LabAssignmentId",
            table: "ExportJobs",
            column: "LabAssignmentId");

        migrationBuilder.AddForeignKey(
            name: "FK_ExportJobs_LabAssignments_LabAssignmentId",
            table: "ExportJobs",
            column: "LabAssignmentId",
            principalTable: "LabAssignments",
            principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ExportJobs_LabAssignments_LabAssignmentId",
            table: "ExportJobs");

        migrationBuilder.DropIndex(
            name: "IX_ExportJobs_LabAssignmentId",
            table: "ExportJobs");

        migrationBuilder.DropColumn(
            name: "LabAssignmentId",
            table: "ExportJobs");
    }
}
