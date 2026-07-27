using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class AddCompetitionHeroImagePath : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HeroImageArtifactId",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.AddColumn<string>(
            name: "HeroImagePath",
            schema: "koc",
            table: "Competitions",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HeroImagePath",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.AddColumn<Guid>(
            name: "HeroImageArtifactId",
            schema: "koc",
            table: "Competitions",
            type: "uniqueidentifier",
            nullable: true);
    }
}
