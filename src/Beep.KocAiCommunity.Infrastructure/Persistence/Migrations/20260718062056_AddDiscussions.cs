using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDiscussions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Discussions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Body = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                AuthorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                VisibilityScope = table.Column<int>(type: "INTEGER", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Discussions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DiscussionReplies",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DiscussionId = table.Column<Guid>(type: "TEXT", nullable: false),
                AuthorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Body = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscussionReplies", x => x.Id);
                table.ForeignKey(
                    name: "FK_DiscussionReplies_Discussions_DiscussionId",
                    column: x => x.DiscussionId,
                    principalSchema: "koc",
                    principalTable: "Discussions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DiscussionReplies_DiscussionId_CreatedUtc",
            schema: "koc",
            table: "DiscussionReplies",
            columns: new[] { "DiscussionId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Discussions_VisibilityScope_VisibilityOrgUnitId",
            schema: "koc",
            table: "Discussions",
            columns: new[] { "VisibilityScope", "VisibilityOrgUnitId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DiscussionReplies",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Discussions",
            schema: "koc");
    }
}
