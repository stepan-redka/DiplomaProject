using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diploma.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<double>(
                name: "ProcessingTimeMs",
                table: "Documents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProcessingTimeMs",
                table: "ChatMessages",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TokenCount",
                table: "ChatMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProcessingTimeMs",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ProcessingTimeMs",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "TokenCount",
                table: "ChatMessages");
        }
    }
}
