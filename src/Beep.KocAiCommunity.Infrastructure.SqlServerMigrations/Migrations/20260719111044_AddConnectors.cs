using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class AddConnectors : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConnectorInstances",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectorCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Endpoint = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                AuthMode = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                DefaultClassification = table.Column<int>(type: "int", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                HealthProbeIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConnectorInstances", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ConnectorHealthSnapshots",
            schema: "platform",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectorInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                LatencyMs = table.Column<int>(type: "int", nullable: false),
                Detail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                MeasuredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConnectorHealthSnapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConnectorHealthSnapshots_ConnectorInstances_ConnectorInstanceId",
                    column: x => x.ConnectorInstanceId,
                    principalSchema: "koc",
                    principalTable: "ConnectorInstances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CredentialVaultEntries",
            schema: "platform",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectorInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                EncryptedValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ProtectionDescriptor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                LastRotatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CredentialVaultEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_CredentialVaultEntries_ConnectorInstances_ConnectorInstanceId",
                    column: x => x.ConnectorInstanceId,
                    principalSchema: "koc",
                    principalTable: "ConnectorInstances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConnectorHealthSnapshots_ConnectorInstanceId_MeasuredUtc",
            schema: "platform",
            table: "ConnectorHealthSnapshots",
            columns: new[] { "ConnectorInstanceId", "MeasuredUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ConnectorInstances_ConnectorCode",
            schema: "koc",
            table: "ConnectorInstances",
            column: "ConnectorCode");

        migrationBuilder.CreateIndex(
            name: "IX_CredentialVaultEntries_ConnectorInstanceId_Key",
            schema: "platform",
            table: "CredentialVaultEntries",
            columns: new[] { "ConnectorInstanceId", "Key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ConnectorHealthSnapshots",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "CredentialVaultEntries",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "ConnectorInstances",
            schema: "koc");
    }
}
