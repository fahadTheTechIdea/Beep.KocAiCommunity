using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCommunityInteractions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsLocked",
            schema: "koc",
            table: "Discussions",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPinned",
            schema: "koc",
            table: "Discussions",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "DiscussionAttachments",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DiscussionId = table.Column<Guid>(type: "TEXT", nullable: false),
                ArtifactReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                UploadedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscussionAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_DiscussionAttachments_Discussions_DiscussionId",
                    column: x => x.DiscussionId,
                    principalSchema: "koc",
                    principalTable: "Discussions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Mentions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                MentionedUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Mentions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Reactions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reactions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DiscussionAttachments_DiscussionId",
            schema: "koc",
            table: "DiscussionAttachments",
            column: "DiscussionId");

        migrationBuilder.CreateIndex(
            name: "IX_Mentions_MentionedUserId_CreatedUtc",
            schema: "koc",
            table: "Mentions",
            columns: new[] { "MentionedUserId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Reactions_TargetType_TargetId",
            schema: "koc",
            table: "Reactions",
            columns: new[] { "TargetType", "TargetId" });

        migrationBuilder.CreateIndex(
            name: "IX_Reactions_TargetType_TargetId_UserId_Emoji",
            schema: "koc",
            table: "Reactions",
            columns: new[] { "TargetType", "TargetId", "UserId", "Emoji" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DiscussionAttachments",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Mentions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Reactions",
            schema: "koc");

        migrationBuilder.DropColumn(
            name: "IsLocked",
            schema: "koc",
            table: "Discussions");

        migrationBuilder.DropColumn(
            name: "IsPinned",
            schema: "koc",
            table: "Discussions");
    }
}
