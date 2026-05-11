using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_A_ExamSession_Participant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradingRound",
                table: "Submissions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasArtifact",
                table: "Submissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParticipantId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GradingJobId",
                table: "QuestionResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradingRound",
                table: "GradingJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ExamSessionId",
                table: "Assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StudentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Participants_ExamSessions_ExamSessionId",
                        column: x => x.ExamSessionId,
                        principalTable: "ExamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ParticipantId_GradingRound",
                table: "Submissions",
                columns: new[] { "ParticipantId", "GradingRound" },
                unique: true,
                filter: "\"ParticipantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResults_GradingJobId_QuestionId",
                table: "QuestionResults",
                columns: new[] { "GradingJobId", "QuestionId" },
                unique: true,
                filter: "\"GradingJobId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ExamSessionId",
                table: "Assignments",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_AssignmentId",
                table: "Participants",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_ExamSessionId_Username",
                table: "Participants",
                columns: new[] { "ExamSessionId", "Username" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_ExamSessions_ExamSessionId",
                table: "Assignments",
                column: "ExamSessionId",
                principalTable: "ExamSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResults_GradingJobs_GradingJobId",
                table: "QuestionResults",
                column: "GradingJobId",
                principalTable: "GradingJobs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Participants_ParticipantId",
                table: "Submissions",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_ExamSessions_ExamSessionId",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResults_GradingJobs_GradingJobId",
                table: "QuestionResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Participants_ParticipantId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "ExamSessions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ParticipantId_GradingRound",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionResults_GradingJobId_QuestionId",
                table: "QuestionResults");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_ExamSessionId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "GradingRound",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "HasArtifact",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "GradingJobId",
                table: "QuestionResults");

            migrationBuilder.DropColumn(
                name: "GradingRound",
                table: "GradingJobs");

            migrationBuilder.DropColumn(
                name: "ExamSessionId",
                table: "Assignments");
        }
    }
}
