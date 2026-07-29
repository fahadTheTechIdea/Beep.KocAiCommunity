using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningTracks_Status_OrderNo",
                schema: "koc",
                table: "LearningTracks");

            migrationBuilder.AddColumn<string>(
                name: "ContentKey",
                schema: "koc",
                table: "LearningTracks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "koc",
                table: "LearningTracks",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTracks_ContentKey_Language",
                schema: "koc",
                table: "LearningTracks",
                columns: new[] { "ContentKey", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningTracks_Language_Status_OrderNo",
                schema: "koc",
                table: "LearningTracks",
                columns: new[] { "Language", "Status", "OrderNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningTracks_ContentKey_Language",
                schema: "koc",
                table: "LearningTracks");

            migrationBuilder.DropIndex(
                name: "IX_LearningTracks_Language_Status_OrderNo",
                schema: "koc",
                table: "LearningTracks");

            migrationBuilder.DropColumn(
                name: "ContentKey",
                schema: "koc",
                table: "LearningTracks");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "koc",
                table: "LearningTracks");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTracks_Status_OrderNo",
                schema: "koc",
                table: "LearningTracks",
                columns: new[] { "Status", "OrderNo" });
        }
    }
}
