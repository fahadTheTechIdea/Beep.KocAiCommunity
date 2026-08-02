using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "koc",
                table: "Submissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Idempotency",
                schema: "koc",
                table: "Submissions",
                columns: new[] { "CompetitionId", "SubmitterUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_Idempotency",
                schema: "koc",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "koc",
                table: "Submissions");
        }
    }
}
