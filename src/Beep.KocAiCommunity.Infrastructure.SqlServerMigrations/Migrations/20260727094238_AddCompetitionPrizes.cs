using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class AddCompetitionPrizes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FirstPrize",
            schema: "koc",
            table: "Competitions",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SecondPrize",
            schema: "koc",
            table: "Competitions",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ThirdPrize",
            schema: "koc",
            table: "Competitions",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FirstPrize",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.DropColumn(
            name: "SecondPrize",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.DropColumn(
            name: "ThirdPrize",
            schema: "koc",
            table: "Competitions");
    }
}
