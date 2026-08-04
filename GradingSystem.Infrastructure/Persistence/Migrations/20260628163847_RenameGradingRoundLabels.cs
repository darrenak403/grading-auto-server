using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameGradingRoundLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Submissions\" SET \"GradingRound\" = REPLACE(\"GradingRound\", 'Lần ', 'Round ') WHERE \"GradingRound\" LIKE 'Lần %';");
            migrationBuilder.Sql(
                "UPDATE \"GradingJobs\" SET \"GradingRound\" = REPLACE(\"GradingRound\", 'Lần ', 'Round ') WHERE \"GradingRound\" LIKE 'Lần %';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Submissions\" SET \"GradingRound\" = REPLACE(\"GradingRound\", 'Round ', 'Lần ') WHERE \"GradingRound\" LIKE 'Round %';");
            migrationBuilder.Sql(
                "UPDATE \"GradingJobs\" SET \"GradingRound\" = REPLACE(\"GradingRound\", 'Round ', 'Lần ') WHERE \"GradingRound\" LIKE 'Round %';");
        }
    }
}
