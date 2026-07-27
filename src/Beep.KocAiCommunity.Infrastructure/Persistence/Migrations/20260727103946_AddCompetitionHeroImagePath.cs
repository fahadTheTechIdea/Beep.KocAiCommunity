using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompetitionHeroImagePath : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "HeroImageArtifactId",
            schema: "koc",
            table: "Competitions",
            newName: "HeroImagePath");

        // The column's meaning changed from an artifact GUID to a web-relative file path; any value
        // carried over by the rename is a stale GUID, not a usable path, so clear it. (SQLite has no
        // schemas, so the table is just "Competitions".)
        migrationBuilder.Sql("UPDATE \"Competitions\" SET \"HeroImagePath\" = NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "HeroImagePath",
            schema: "koc",
            table: "Competitions",
            newName: "HeroImageArtifactId");
    }
}
