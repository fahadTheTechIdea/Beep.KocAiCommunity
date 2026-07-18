using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notifications",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                LinkUrl = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ReadUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId_ReadUtc_CreatedUtc",
            schema: "koc",
            table: "Notifications",
            columns: new[] { "UserId", "ReadUtc", "CreatedUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications",
            schema: "koc");
    }
}
