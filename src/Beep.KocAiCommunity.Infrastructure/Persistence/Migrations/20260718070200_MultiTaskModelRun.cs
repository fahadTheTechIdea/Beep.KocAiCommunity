using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MultiTaskModelRun : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "AreaUnderRoc",
            schema: "koc",
            table: "ModelRuns",
            newName: "SecondaryValue");

        migrationBuilder.RenameColumn(
            name: "Accuracy",
            schema: "koc",
            table: "ModelRuns",
            newName: "PrimaryValue");

        migrationBuilder.AddColumn<string>(
            name: "PrimaryMetric",
            schema: "koc",
            table: "ModelRuns",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "SecondaryMetric",
            schema: "koc",
            table: "ModelRuns",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Task",
            schema: "koc",
            table: "ModelRuns",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PrimaryMetric",
            schema: "koc",
            table: "ModelRuns");

        migrationBuilder.DropColumn(
            name: "SecondaryMetric",
            schema: "koc",
            table: "ModelRuns");

        migrationBuilder.DropColumn(
            name: "Task",
            schema: "koc",
            table: "ModelRuns");

        migrationBuilder.RenameColumn(
            name: "SecondaryValue",
            schema: "koc",
            table: "ModelRuns",
            newName: "AreaUnderRoc");

        migrationBuilder.RenameColumn(
            name: "PrimaryValue",
            schema: "koc",
            table: "ModelRuns",
            newName: "Accuracy");
    }
}
