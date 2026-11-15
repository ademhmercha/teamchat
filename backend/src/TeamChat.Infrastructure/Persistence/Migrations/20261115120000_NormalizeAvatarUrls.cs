using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamChat.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(TeamChatDbContext))]
    [Migration("20261115120000_NormalizeAvatarUrls")]
    public partial class NormalizeAvatarUrls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"AspNetUsers\" " +
                "SET \"AvatarUrl\" = regexp_replace(\"AvatarUrl\", '^https?://[^/]+(/api/users/[0-9a-fA-F-]{36}/avatar).*$', '\\1', 'i') " +
                "WHERE \"AvatarUrl\" ~* '^https?://[^/]+/api/users/[0-9a-fA-F-]{36}/avatar';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
