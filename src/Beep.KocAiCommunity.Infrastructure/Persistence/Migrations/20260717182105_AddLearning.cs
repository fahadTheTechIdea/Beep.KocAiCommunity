using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLearning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LearningTracks",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Summary = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                OrderNo = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Domain = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                VisibilityScope = table.Column<int>(type: "INTEGER", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                RecommendedCompetitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LearningTracks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LessonProgress",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EnrollmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                LessonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LessonProgress", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackCompletions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackCompletions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackEnrollments",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackEnrollments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Lessons",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                OrderNo = table.Column<int>(type: "INTEGER", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ContentRef = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                EstimatedMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                HandsOnKind = table.Column<string>(type: "TEXT", nullable: true),
                HandsOnRefId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Lessons", x => x.Id);
                table.ForeignKey(
                    name: "FK_Lessons_LearningTracks_TrackId",
                    column: x => x.TrackId,
                    principalSchema: "koc",
                    principalTable: "LearningTracks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LearningTracks_Status_OrderNo",
            schema: "koc",
            table: "LearningTracks",
            columns: new[] { "Status", "OrderNo" });

        migrationBuilder.CreateIndex(
            name: "IX_LessonProgress_EnrollmentId_LessonId",
            schema: "koc",
            table: "LessonProgress",
            columns: new[] { "EnrollmentId", "LessonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Lessons_TrackId_OrderNo",
            schema: "koc",
            table: "Lessons",
            columns: new[] { "TrackId", "OrderNo" });

        migrationBuilder.CreateIndex(
            name: "IX_TrackCompletions_TrackId_UserId",
            schema: "koc",
            table: "TrackCompletions",
            columns: new[] { "TrackId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TrackCompletions_UserId_CompletedUtc",
            schema: "koc",
            table: "TrackCompletions",
            columns: new[] { "UserId", "CompletedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_TrackEnrollments_TrackId_UserId",
            schema: "koc",
            table: "TrackEnrollments",
            columns: new[] { "TrackId", "UserId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LessonProgress",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Lessons",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "TrackCompletions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "TrackEnrollments",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "LearningTracks",
            schema: "koc");
    }
}
