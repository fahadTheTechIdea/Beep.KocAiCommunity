using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddModelRegistry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RegisteredModels",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegisteredModels", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ModelVersions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                SemVer = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                SourceRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                MetricName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                MetricValue = table.Column<double>(type: "REAL", nullable: false),
                RegisteredByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ModelVersions_RegisteredModels_ModelId",
                    column: x => x.ModelId,
                    principalSchema: "koc",
                    principalTable: "RegisteredModels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ModelApprovals",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ModelVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                ApproverUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Decision = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelApprovals", x => x.Id);
                table.ForeignKey(
                    name: "FK_ModelApprovals_ModelVersions_ModelVersionId",
                    column: x => x.ModelVersionId,
                    principalSchema: "koc",
                    principalTable: "ModelVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ModelApprovals_ModelVersionId_ApproverUserId",
            schema: "koc",
            table: "ModelApprovals",
            columns: new[] { "ModelVersionId", "ApproverUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ModelVersions_ModelId_SemVer",
            schema: "koc",
            table: "ModelVersions",
            columns: new[] { "ModelId", "SemVer" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RegisteredModels_Name",
            schema: "koc",
            table: "RegisteredModels",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ModelApprovals",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "ModelVersions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "RegisteredModels",
            schema: "koc");
    }
}
