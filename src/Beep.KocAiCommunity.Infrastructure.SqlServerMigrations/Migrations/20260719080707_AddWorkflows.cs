using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class AddWorkflows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Workflows",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Classification = table.Column<int>(type: "int", nullable: false),
                LatestVersionNumber = table.Column<int>(type: "int", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workflows", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowTemplates",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                Domain = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SchemaVersion = table.Column<int>(type: "int", nullable: false),
                SnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowTemplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowVersions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                SchemaVersion = table.Column<int>(type: "int", nullable: false),
                DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                PublishedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowVersions_Workflows_WorkflowId",
                    column: x => x.WorkflowId,
                    principalSchema: "koc",
                    principalTable: "Workflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Workflows_OwnerUserId",
            schema: "koc",
            table: "Workflows",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowTemplates_Code",
            schema: "koc",
            table: "WorkflowTemplates",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowVersions_WorkflowId_VersionNumber",
            schema: "koc",
            table: "WorkflowVersions",
            columns: new[] { "WorkflowId", "VersionNumber" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "WorkflowTemplates",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "WorkflowVersions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Workflows",
            schema: "koc");
    }
}
