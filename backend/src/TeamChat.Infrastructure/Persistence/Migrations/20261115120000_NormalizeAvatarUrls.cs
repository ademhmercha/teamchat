using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamChat.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Correctif de donnees : d'anciennes lignes contiennent l'avatar sous forme d'URL absolue
    /// (http://ec2-....amazonaws.com/api/users/{id}/avatar), generee a partir de l'hote de l'EC2 derriere
    /// CloudFront et bloquee par le navigateur (Mixed Content). On ne garde que le chemin relatif.
    /// Les URL externes (autre chemin que /api/users/{id}/avatar) ne sont pas touchees. Aucun changement de schema.
    /// </summary>
    [DbContext(typeof(TeamChatDbContext))]
    [Migration("20261115120000_NormalizeAvatarUrls")]
    public partial class NormalizeAvatarUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"AspNetUsers\" " +
                "SET \"AvatarUrl\" = regexp_replace(\"AvatarUrl\", '^https?://[^/]+(/api/users/[0-9a-fA-F-]{36}/avatar).*$', '\\1', 'i') " +
                "WHERE \"AvatarUrl\" ~* '^https?://[^/]+/api/users/[0-9a-fA-F-]{36}/avatar';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible : l'hote d'origine n'etait pas conserve (et ne doit plus l'etre).
        }
    }
}
