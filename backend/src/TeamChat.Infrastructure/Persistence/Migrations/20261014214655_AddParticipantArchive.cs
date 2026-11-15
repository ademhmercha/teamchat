using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamChat.Infrastructure.Persistence.Migrations
{
    public partial class AddParticipantArchive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Archived",
                table: "ConversationParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Archived",
                table: "ConversationParticipants");
        }
    }
}
