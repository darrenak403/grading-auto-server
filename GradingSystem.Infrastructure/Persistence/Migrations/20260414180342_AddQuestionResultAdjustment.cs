using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionResultAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustReason",
                table: "QuestionResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdjustedAt",
                table: "QuestionResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustedBy",
                table: "QuestionResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdjustedScore",
                table: "QuestionResults",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustReason",
                table: "QuestionResults");

            migrationBuilder.DropColumn(
                name: "AdjustedAt",
                table: "QuestionResults");

            migrationBuilder.DropColumn(
                name: "AdjustedBy",
                table: "QuestionResults");

            migrationBuilder.DropColumn(
                name: "AdjustedScore",
                table: "QuestionResults");
        }
    }
}
