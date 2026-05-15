using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diploma.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectedModelToChatSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedModel",
                table: "ChatSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedModel",
                table: "ChatSessions");
        }
    }
}
