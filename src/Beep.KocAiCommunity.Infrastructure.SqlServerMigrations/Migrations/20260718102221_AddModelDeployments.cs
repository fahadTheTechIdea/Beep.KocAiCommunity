using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class AddModelDeployments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ModelDeployments",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModelVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Environment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                DeployedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                DeployedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelDeployments", x => x.Id);
                table.ForeignKey(
                    name: "FK_ModelDeployments_ModelVersions_ModelVersionId",
                    column: x => x.ModelVersionId,
                    principalSchema: "koc",
                    principalTable: "ModelVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ModelDeployments_ModelVersionId",
            schema: "koc",
            table: "ModelDeployments",
            column: "ModelVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_ModelDeployments_Status_Environment",
            schema: "koc",
            table: "ModelDeployments",
            columns: new[] { "Status", "Environment" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ModelDeployments",
            schema: "koc");
    }
}
