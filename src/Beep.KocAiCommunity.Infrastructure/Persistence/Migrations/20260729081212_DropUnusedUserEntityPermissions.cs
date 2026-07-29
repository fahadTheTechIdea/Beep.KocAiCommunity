using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedUserEntityPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEntityPermissions",
                schema: "koc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEntityPermissions",
                schema: "koc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GrantedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    GrantedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PermissionKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEntityPermissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEntityPermissions_UserId_ResourceType_ResourceId",
                schema: "koc",
                table: "UserEntityPermissions",
                columns: new[] { "UserId", "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEntityPermissions_UserId_ResourceType_ResourceId_PermissionKey",
                schema: "koc",
                table: "UserEntityPermissions",
                columns: new[] { "UserId", "ResourceType", "ResourceId", "PermissionKey" },
                unique: true);
        }
    }
}
