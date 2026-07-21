using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

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
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DepartmentId",
            schema: "koc",
            table: "UserProfiles",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Email",
            schema: "koc",
            table: "UserProfiles",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "OrgUnitId",
            schema: "koc",
            table: "UserProfiles",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Code",
            schema: "koc",
            table: "OrgUnits",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CompetitionCreatorGrants",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                MaxScope = table.Column<int>(type: "int", nullable: false),
                GrantedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
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
