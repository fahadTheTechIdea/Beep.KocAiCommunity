using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompetitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                LabelColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                TaskType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
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
