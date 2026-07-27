using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompetitionHeroImage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "HeroImageArtifactId",
            schema: "koc",
            table: "Competitions",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HeroImageArtifactId",
            schema: "koc",
            table: "Competitions");
    }
}
