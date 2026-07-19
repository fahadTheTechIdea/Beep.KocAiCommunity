using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                BestRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExperimentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ParentRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RunByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                FailureStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Task = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Algorithm = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                PrimaryMetric = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                PrimaryValue = table.Column<double>(type: "float", nullable: true),
                SecondaryMetric = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                SecondaryValue = table.Column<double>(type: "float", nullable: true),
                RowCount = table.Column<long>(type: "bigint", nullable: false),
                TrialCount = table.Column<int>(type: "int", nullable: false),
                HyperparametersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                EnvironmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DatasetSnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                IsBest = table.Column<bool>(type: "bit", nullable: false),
                Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Value = table.Column<double>(type: "float", nullable: false),
                Dataset = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                Phase = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                Step = table.Column<int>(type: "int", nullable: false),
                LoggedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ValueJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
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
