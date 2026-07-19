using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddExperiments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Experiments",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                BestRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                Tags = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Experiments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ExperimentRuns",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ExperimentId = table.Column<Guid>(type: "TEXT", nullable: false),
                ParentRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                DatasetId = table.Column<Guid>(type: "TEXT", nullable: true),
                RunByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                FailureStage = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Task = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                Algorithm = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                PrimaryMetric = table.Column<string>(type: "TEXT", maxLength: 48, nullable: true),
                PrimaryValue = table.Column<double>(type: "REAL", nullable: true),
                SecondaryMetric = table.Column<string>(type: "TEXT", maxLength: 48, nullable: true),
                SecondaryValue = table.Column<double>(type: "REAL", nullable: true),
                RowCount = table.Column<long>(type: "INTEGER", nullable: false),
                TrialCount = table.Column<int>(type: "INTEGER", nullable: false),
                HyperparametersJson = table.Column<string>(type: "TEXT", nullable: true),
                EnvironmentJson = table.Column<string>(type: "TEXT", nullable: true),
                DatasetSnapshotHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                IsBest = table.Column<bool>(type: "INTEGER", nullable: false),
                Tags = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExperimentRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExperimentRuns_Experiments_ExperimentId",
                    column: x => x.ExperimentId,
                    principalSchema: "koc",
                    principalTable: "Experiments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RunMetrics",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Value = table.Column<double>(type: "REAL", nullable: false),
                Dataset = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                Phase = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                Step = table.Column<int>(type: "INTEGER", nullable: false),
                LoggedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RunMetrics", x => x.Id);
                table.ForeignKey(
                    name: "FK_RunMetrics_ExperimentRuns_RunId",
                    column: x => x.RunId,
                    principalSchema: "koc",
                    principalTable: "ExperimentRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RunParameters",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                ValueJson = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RunParameters", x => x.Id);
                table.ForeignKey(
                    name: "FK_RunParameters_ExperimentRuns_RunId",
                    column: x => x.RunId,
                    principalSchema: "koc",
                    principalTable: "ExperimentRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExperimentRuns_ExperimentId_CreatedUtc",
            schema: "koc",
            table: "ExperimentRuns",
            columns: new[] { "ExperimentId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Experiments_OwnerUserId_CreatedUtc",
            schema: "koc",
            table: "Experiments",
            columns: new[] { "OwnerUserId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_RunMetrics_RunId_Step",
            schema: "koc",
            table: "RunMetrics",
            columns: new[] { "RunId", "Step" });

        migrationBuilder.CreateIndex(
            name: "IX_RunParameters_RunId_Name",
            schema: "koc",
            table: "RunParameters",
            columns: new[] { "RunId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RunMetrics",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "RunParameters",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "ExperimentRuns",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Experiments",
            schema: "koc");
    }
}
