using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompetitionDatasets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "EvaluationArtifactId",
            schema: "koc",
            table: "Competitions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "IdColumn",
            schema: "koc",
            table: "Competitions",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "LabelColumn",
            schema: "koc",
            table: "Competitions",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TaskType",
            schema: "koc",
            table: "Competitions",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "TrainingDatasetArtifactId",
            schema: "koc",
            table: "Competitions",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EvaluationArtifactId",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.DropColumn(
            name: "IdColumn",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.DropColumn(
            name: "LabelColumn",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.DropColumn(
            name: "TaskType",
            schema: "koc",
            table: "Competitions");

        migrationBuilder.DropColumn(
            name: "TrainingDatasetArtifactId",
            schema: "koc",
            table: "Competitions");
    }
}
