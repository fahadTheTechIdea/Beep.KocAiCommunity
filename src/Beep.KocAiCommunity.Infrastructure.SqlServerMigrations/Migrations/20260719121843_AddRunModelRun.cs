using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class AddRunModelRun : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ModelRunId",
            schema: "koc",
            table: "ExperimentRuns",
            type: "uniqueidentifier",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ModelRunId",
            schema: "koc",
            table: "ExperimentRuns");
    }
}
