using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConnectorCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Endpoint = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                AuthMode = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                DefaultClassification = table.Column<int>(type: "INTEGER", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                HealthProbeIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConnectorInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                Detail = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                MeasuredUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConnectorInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                EncryptedValue = table.Column<string>(type: "TEXT", nullable: false),
                ProtectionDescriptor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                LastRotatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
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
