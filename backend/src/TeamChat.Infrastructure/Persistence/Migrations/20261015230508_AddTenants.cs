using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            // Societe par defaut : toutes les donnees existantes lui sont rattachees.
            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "Name", "Slug", "IsActive", "CreatedAt" },
                values: new object[]
                {
                    new Guid("00000000-0000-0000-0000-000000000001"),
                    "TeamChat",
                    "teamchat",
                    true,
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                });

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CallLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            // Rattachement des donnees et comptes existants a la societe par defaut.
            const string defaultTenant = "00000000-0000-0000-0000-000000000001";
            migrationBuilder.Sql($"UPDATE \"Notifications\" SET \"TenantId\" = '{defaultTenant}' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql($"UPDATE \"Messages\" SET \"TenantId\" = '{defaultTenant}' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql($"UPDATE \"Conversations\" SET \"TenantId\" = '{defaultTenant}' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql($"UPDATE \"CallLogs\" SET \"TenantId\" = '{defaultTenant}' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql($"UPDATE \"Attachments\" SET \"TenantId\" = '{defaultTenant}' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql($"UPDATE \"AspNetUsers\" SET \"TenantId\" = '{defaultTenant}' WHERE \"TenantId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Messages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "CallLogs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Attachments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId",
                table: "Messages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId",
                table: "Conversations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_TenantId",
                table: "CallLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId",
                table: "Attachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_CallLogs_TenantId",
                table: "CallLogs");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_TenantId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CallLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUsers");
        }
    }
}
