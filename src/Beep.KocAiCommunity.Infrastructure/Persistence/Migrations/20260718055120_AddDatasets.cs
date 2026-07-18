using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDatasets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Datasets",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                VisibilityScope = table.Column<int>(type: "INTEGER", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                Classification = table.Column<int>(type: "INTEGER", nullable: false),
                Domain = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Tags = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                FileArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Datasets", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Datasets_OwnerUserId",
            schema: "koc",
            table: "Datasets",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Datasets_VisibilityScope_VisibilityOrgUnitId",
            schema: "koc",
            table: "Datasets",
            columns: new[] { "VisibilityScope", "VisibilityOrgUnitId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Datasets",
            schema: "koc");
    }
}
