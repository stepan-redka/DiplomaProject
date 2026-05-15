using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diploma.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedDocumentIdsToChatSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "RelatedDocumentIds",
                table: "ChatSessions",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedDocumentIds",
                table: "ChatSessions");
        }
    }
}
