using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEngagement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ActivityEvents",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                RefType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                RefId = table.Column<Guid>(type: "TEXT", nullable: true),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                VisibilityScope = table.Column<int>(type: "INTEGER", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Badges",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                IconFile = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Tier = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Badges", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Kudos",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FromUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ToUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                RefType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                RefId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Kudos", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserBadges",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                BadgeCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                RefId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserBadges", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserProfiles",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Bio = table.Column<string>(type: "TEXT", maxLength: 280, nullable: true),
                AvatarIcon = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                SkillsCsv = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                XpTotal = table.Column<int>(type: "INTEGER", nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                CurrentStreakDays = table.Column<int>(type: "INTEGER", nullable: false),
                LongestStreakDays = table.Column<int>(type: "INTEGER", nullable: false),
                LastActiveDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserProfiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "XpEvents",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Points = table.Column<int>(type: "INTEGER", nullable: false),
                RefType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                RefId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_XpEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityEvents_CreatedUtc",
            schema: "koc",
            table: "ActivityEvents",
            column: "CreatedUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityEvents_VisibilityOrgUnitId_CreatedUtc",
            schema: "koc",
            table: "ActivityEvents",
            columns: new[] { "VisibilityOrgUnitId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Badges_Code",
            schema: "koc",
            table: "Badges",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Kudos_FromUserId_CreatedUtc",
            schema: "koc",
            table: "Kudos",
            columns: new[] { "FromUserId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Kudos_ToUserId_CreatedUtc",
            schema: "koc",
            table: "Kudos",
            columns: new[] { "ToUserId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_UserBadges_UserId_BadgeCode",
            schema: "koc",
            table: "UserBadges",
            columns: new[] { "UserId", "BadgeCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_UserId",
            schema: "koc",
            table: "UserProfiles",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_XpTotal",
            schema: "koc",
            table: "UserProfiles",
            column: "XpTotal");

        migrationBuilder.CreateIndex(
            name: "IX_XpEvents_CreatedUtc",
            schema: "koc",
            table: "XpEvents",
            column: "CreatedUtc");

        migrationBuilder.CreateIndex(
            name: "IX_XpEvents_UserId_Source_RefId",
            schema: "koc",
            table: "XpEvents",
            columns: new[] { "UserId", "Source", "RefId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ActivityEvents",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Badges",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Kudos",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "UserBadges",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "UserProfiles",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "XpEvents",
            schema: "koc");
    }
}
