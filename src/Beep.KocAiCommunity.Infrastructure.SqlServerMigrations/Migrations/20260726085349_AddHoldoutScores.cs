using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

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
            type: "float",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "PrivateScore",
            schema: "koc",
            table: "LeaderboardEntries",
            type: "float",
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
