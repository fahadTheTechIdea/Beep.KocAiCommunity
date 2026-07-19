using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDatasetVersioning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "LatestVersionNumber",
            schema: "koc",
            table: "Datasets",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "LicenseSpdxId",
            schema: "koc",
            table: "Datasets",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DatasetVersions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DatasetId = table.Column<Guid>(type: "TEXT", nullable: false),
                VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                TotalSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                PublishedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatasetVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_DatasetVersions_Datasets_DatasetId",
                    column: x => x.DatasetId,
                    principalSchema: "koc",
                    principalTable: "Datasets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DatasetFiles",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DatasetVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                ArtifactReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                LogicalPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                RowCount = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatasetFiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_DatasetFiles_DatasetVersions_DatasetVersionId",
                    column: x => x.DatasetVersionId,
                    principalSchema: "koc",
                    principalTable: "DatasetVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DatasetProfiles",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DatasetVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                SampledRows = table.Column<long>(type: "INTEGER", nullable: false),
                TotalRows = table.Column<long>(type: "INTEGER", nullable: false),
                GeneratedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatasetProfiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_DatasetProfiles_DatasetVersions_DatasetVersionId",
                    column: x => x.DatasetVersionId,
                    principalSchema: "koc",
                    principalTable: "DatasetVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DatasetSchemaColumns",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DatasetVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                ColumnName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                DataType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Nullable = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatasetSchemaColumns", x => x.Id);
                table.ForeignKey(
                    name: "FK_DatasetSchemaColumns_DatasetVersions_DatasetVersionId",
                    column: x => x.DatasetVersionId,
                    principalSchema: "koc",
                    principalTable: "DatasetVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DatasetProfileColumns",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DatasetProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                ColumnName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                NullCount = table.Column<long>(type: "INTEGER", nullable: false),
                DistinctCount = table.Column<long>(type: "INTEGER", nullable: false),
                Min = table.Column<double>(type: "REAL", nullable: true),
                Max = table.Column<double>(type: "REAL", nullable: true),
                Mean = table.Column<double>(type: "REAL", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatasetProfileColumns", x => x.Id);
                table.ForeignKey(
                    name: "FK_DatasetProfileColumns_DatasetProfiles_DatasetProfileId",
                    column: x => x.DatasetProfileId,
                    principalSchema: "koc",
                    principalTable: "DatasetProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DatasetFiles_DatasetVersionId",
            schema: "koc",
            table: "DatasetFiles",
            column: "DatasetVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_DatasetProfileColumns_DatasetProfileId",
            schema: "koc",
            table: "DatasetProfileColumns",
            column: "DatasetProfileId");

        migrationBuilder.CreateIndex(
            name: "IX_DatasetProfiles_DatasetVersionId",
            schema: "koc",
            table: "DatasetProfiles",
            column: "DatasetVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_DatasetSchemaColumns_DatasetVersionId",
            schema: "koc",
            table: "DatasetSchemaColumns",
            column: "DatasetVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_DatasetVersions_DatasetId_VersionNumber",
            schema: "koc",
            table: "DatasetVersions",
            columns: new[] { "DatasetId", "VersionNumber" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DatasetFiles",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "DatasetProfileColumns",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "DatasetSchemaColumns",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "DatasetProfiles",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "DatasetVersions",
            schema: "koc");

        migrationBuilder.DropColumn(
            name: "LatestVersionNumber",
            schema: "koc",
            table: "Datasets");

        migrationBuilder.DropColumn(
            name: "LicenseSpdxId",
            schema: "koc",
            table: "Datasets");
    }
}
