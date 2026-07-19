using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddJobs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Jobs",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                Priority = table.Column<int>(type: "INTEGER", nullable: false),
                AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                MaxAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                LeaseOwnerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                LeaseExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                NextAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastError = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                CancelRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Jobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "JobAttempts",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                Error = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                WorkerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobAttempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobAttempts_Jobs_JobId",
                    column: x => x.JobId,
                    principalSchema: "koc",
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "JobLogs",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                LoggedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobLogs_Jobs_JobId",
                    column: x => x.JobId,
                    principalSchema: "koc",
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_JobAttempts_JobId_AttemptNumber",
            schema: "koc",
            table: "JobAttempts",
            columns: new[] { "JobId", "AttemptNumber" });

        migrationBuilder.CreateIndex(
            name: "IX_JobLogs_JobId_LoggedUtc",
            schema: "koc",
            table: "JobLogs",
            columns: new[] { "JobId", "LoggedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Jobs_OwnerUserId_CreatedUtc",
            schema: "koc",
            table: "Jobs",
            columns: new[] { "OwnerUserId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Jobs_Status_NextAttemptUtc_LeaseExpiresUtc",
            schema: "koc",
            table: "Jobs",
            columns: new[] { "Status", "NextAttemptUtc", "LeaseExpiresUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "JobAttempts",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "JobLogs",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Jobs",
            schema: "koc");
    }
}
