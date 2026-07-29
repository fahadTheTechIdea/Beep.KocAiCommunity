using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "koc",
                table: "UserProfiles",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                schema: "koc",
                table: "UserProfiles");
        }
    }
}
