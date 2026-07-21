using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class SimplifyUserProfileDepartment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserProfiles_OrgUnitId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.DropColumn(
            name: "OrgUnitId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_DepartmentId",
            schema: "koc",
            table: "UserProfiles",
            column: "DepartmentId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserProfiles_DepartmentId",
            schema: "koc",
            table: "UserProfiles");

        migrationBuilder.AddColumn<Guid>(
            name: "OrgUnitId",
            schema: "koc",
            table: "UserProfiles",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_OrgUnitId",
            schema: "koc",
            table: "UserProfiles",
            column: "OrgUnitId");
    }
}
