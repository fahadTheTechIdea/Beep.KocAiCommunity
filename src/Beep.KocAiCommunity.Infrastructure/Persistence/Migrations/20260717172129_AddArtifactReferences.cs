using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddArtifactReferences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArtifactReferences",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                StorageKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                LogicalPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Classification = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArtifactReferences", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactReferences_Sha256",
            schema: "koc",
            table: "ArtifactReferences",
            column: "Sha256",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactReferences_StorageKey",
            schema: "koc",
            table: "ArtifactReferences",
            column: "StorageKey");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ArtifactReferences",
            schema: "koc");
    }
}
