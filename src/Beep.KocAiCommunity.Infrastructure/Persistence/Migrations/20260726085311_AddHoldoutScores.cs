using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHoldoutScores : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "PrivateScore",
            schema: "koc",
            table: "Submissions",
            type: "REAL",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "PrivateScore",
            schema: "koc",
            table: "LeaderboardEntries",
            type: "REAL",
            nullable: false,
            defaultValue: 0.0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PrivateScore",
            schema: "koc",
            table: "Submissions");

        migrationBuilder.DropColumn(
            name: "PrivateScore",
            schema: "koc",
            table: "LeaderboardEntries");
    }
}
