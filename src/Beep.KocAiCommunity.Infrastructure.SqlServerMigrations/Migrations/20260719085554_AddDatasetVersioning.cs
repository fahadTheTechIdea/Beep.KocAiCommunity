using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

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
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "LicenseSpdxId",
            schema: "koc",
            table: "Datasets",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DatasetVersions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                TotalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ArtifactReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LogicalPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                RowCount = table.Column<long>(type: "bigint", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SampledRows = table.Column<long>(type: "bigint", nullable: false),
                TotalRows = table.Column<long>(type: "bigint", nullable: false),
                GeneratedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Ordinal = table.Column<int>(type: "int", nullable: false),
                ColumnName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                DataType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Nullable = table.Column<bool>(type: "bit", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ColumnName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                NullCount = table.Column<long>(type: "bigint", nullable: false),
                DistinctCount = table.Column<long>(type: "bigint", nullable: false),
                Min = table.Column<double>(type: "float", nullable: true),
                Max = table.Column<double>(type: "float", nullable: true),
                Mean = table.Column<double>(type: "float", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
