using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddModelRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ModelRuns",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DatasetName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                LabelColumn = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Algorithm = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Accuracy = table.Column<double>(type: "REAL", nullable: false),
                AreaUnderRoc = table.Column<double>(type: "REAL", nullable: false),
                RowCount = table.Column<long>(type: "INTEGER", nullable: false),
                RunByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ModelRuns_RunByUserId_CompletedUtc",
            schema: "koc",
            table: "ModelRuns",
            columns: new[] { "RunByUserId", "CompletedUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ModelRuns",
            schema: "koc");
    }
}
