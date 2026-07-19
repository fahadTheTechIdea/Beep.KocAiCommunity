using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

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
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ModelArtifactId",
            schema: "koc",
            table: "ModelRuns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ModelInferenceLogs",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModelVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CallerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Endpoint = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                RowCount = table.Column<int>(type: "int", nullable: false),
                LatencyMs = table.Column<int>(type: "int", nullable: false),
                CalledUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Success = table.Column<bool>(type: "bit", nullable: false),
                Error = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
