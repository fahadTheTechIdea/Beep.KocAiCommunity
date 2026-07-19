using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInference : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FeatureStatsJson",
            schema: "koc",
            table: "ModelRuns",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ModelArtifactId",
            schema: "koc",
            table: "ModelRuns",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ModelInferenceLogs",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ModelVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                CallerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Endpoint = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                CalledUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Success = table.Column<bool>(type: "INTEGER", nullable: false),
                Error = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelInferenceLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ModelInferenceLogs_ModelVersions_ModelVersionId",
                    column: x => x.ModelVersionId,
                    principalSchema: "koc",
                    principalTable: "ModelVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ModelInferenceLogs_ModelVersionId_CalledUtc",
            schema: "koc",
            table: "ModelInferenceLogs",
            columns: new[] { "ModelVersionId", "CalledUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ModelInferenceLogs",
            schema: "koc");

        migrationBuilder.DropColumn(
            name: "FeatureStatsJson",
            schema: "koc",
            table: "ModelRuns");

        migrationBuilder.DropColumn(
            name: "ModelArtifactId",
            schema: "koc",
            table: "ModelRuns");
    }
}
