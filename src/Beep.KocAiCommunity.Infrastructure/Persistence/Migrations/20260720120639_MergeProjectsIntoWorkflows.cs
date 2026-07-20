using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MergeProjectsIntoWorkflows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Projects",
            schema: "koc");

        migrationBuilder.RenameColumn(
            name: "ProjectId",
            schema: "koc",
            table: "Workflows",
            newName: "CompetitionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "CompetitionId",
            schema: "koc",
            table: "Workflows",
            newName: "ProjectId");

        migrationBuilder.CreateTable(
            name: "Projects",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompetitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                LabelColumn = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                TaskType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_OwnerUserId_CreatedUtc",
            schema: "koc",
            table: "Projects",
            columns: new[] { "OwnerUserId", "CreatedUtc" });
    }
}
