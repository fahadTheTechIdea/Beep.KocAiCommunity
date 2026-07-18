using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompetitions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Competitions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                VisibilityScope = table.Column<int>(type: "INTEGER", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                RevealUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                SubmissionQuotaPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                ScorerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                AnswerKeyArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                RecommendedTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Competitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LeaderboardEntries",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompetitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                SubmitterUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                BestSubmissionId = table.Column<Guid>(type: "TEXT", nullable: true),
                Score = table.Column<double>(type: "REAL", nullable: false),
                Rank = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaderboardEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_LeaderboardEntries_Competitions_CompetitionId",
                    column: x => x.CompetitionId,
                    principalSchema: "koc",
                    principalTable: "Competitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Submissions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompetitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                SubmitterUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                PredictionArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                SubmittedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Score = table.Column<double>(type: "REAL", nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Submissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Submissions_Competitions_CompetitionId",
                    column: x => x.CompetitionId,
                    principalSchema: "koc",
                    principalTable: "Competitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Competitions_Status_VisibilityScope",
            schema: "koc",
            table: "Competitions",
            columns: new[] { "Status", "VisibilityScope" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardEntries_CompetitionId_Rank",
            schema: "koc",
            table: "LeaderboardEntries",
            columns: new[] { "CompetitionId", "Rank" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardEntries_CompetitionId_SubmitterUserId",
            schema: "koc",
            table: "LeaderboardEntries",
            columns: new[] { "CompetitionId", "SubmitterUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Submissions_CompetitionId_SubmitterUserId_SubmittedUtc",
            schema: "koc",
            table: "Submissions",
            columns: new[] { "CompetitionId", "SubmitterUserId", "SubmittedUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LeaderboardEntries",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Submissions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Competitions",
            schema: "koc");
    }
}
