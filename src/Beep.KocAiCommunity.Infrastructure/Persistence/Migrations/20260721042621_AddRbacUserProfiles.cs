using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRbacUserProfiles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CompanyId",
            schema: "koc",
            table: "UserProfiles",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DepartmentId",
            schema: "koc",
            table: "UserProfiles",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Email",
            schema: "koc",
            table: "UserProfiles",
            type: "TEXT",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "OrgUnitId",
            schema: "koc",
            table: "UserProfiles",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Code",
            schema: "koc",
            table: "OrgUnits",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CompetitionCreatorGrants",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                MaxScope = table.Column<int>(type: "INTEGER", nullable: false),
                GrantedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
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
                table.PrimaryKey("PK_CompetitionCreatorGrants", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_Email",
            schema: "koc",
            table: "UserProfiles",
            column: "Email");

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_OrgUnitId",
            schema: "koc",
            table: "UserProfiles",
            column: "OrgUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_OrgUnits_Code",
            schema: "koc",
            table: "OrgUnits",
            column: "Code");

        migrationBuilder.CreateIndex(
            name: "IX_CompetitionCreatorGrants_UserId",
            schema: "koc",
            table: "CompetitionCreatorGrants",
            column: "UserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CompetitionCreatorGrants",
            schema: "koc");

        migrationBuilder.DropIndex(
            name: "IX_UserProfiles_Email",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropIndex(
            name: "IX_UserProfiles_OrgUnitId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropIndex(
            name: "IX_OrgUnits_Code",
            schema: "koc",
            table: "OrgUnits");

        migrationBuilder.DropColumn(
            name: "CompanyId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropColumn(
            name: "DepartmentId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropColumn(
            name: "Email",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropColumn(
            name: "OrgUnitId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropColumn(
            name: "Code",
            schema: "koc",
            table: "OrgUnits");
    }
}
