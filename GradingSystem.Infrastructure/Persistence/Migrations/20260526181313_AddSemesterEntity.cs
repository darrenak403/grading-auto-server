using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSemesterEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SemesterId",
                table: "LabAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Semesters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Semesters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabAssignments_SemesterId",
                table: "LabAssignments",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_Code",
                table: "Semesters",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LabAssignments_Semesters_SemesterId",
                table: "LabAssignments",
                column: "SemesterId",
                principalTable: "Semesters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabAssignments_Semesters_SemesterId",
                table: "LabAssignments");

            migrationBuilder.DropTable(
                name: "Semesters");

            migrationBuilder.DropIndex(
                name: "IX_LabAssignments_SemesterId",
                table: "LabAssignments");

            migrationBuilder.DropColumn(
                name: "SemesterId",
                table: "LabAssignments");
        }
    }
}
