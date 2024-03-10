using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PocketDockUI.Data.Migrations
{
    public partial class AddNewFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domain",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "TriggerIpAddress",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "TriggerPort",
                table: "Server");

            migrationBuilder.AddColumn<int>(
                name: "ProxyId",
                table: "ServerAssignment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemporaryServer",
                table: "Server",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Server",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Proxy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppName = table.Column<string>(type: "text", nullable: true),
                    Region = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    ServerId = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proxy", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerAssignment_ProxyId",
                table: "ServerAssignment",
                column: "ProxyId");

            migrationBuilder.CreateIndex(
                name: "IX_Proxy_Region",
                table: "Proxy",
                column: "Region",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerAssignment_Proxy_ProxyId",
                table: "ServerAssignment",
                column: "ProxyId",
                principalTable: "Proxy",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerAssignment_Proxy_ProxyId",
                table: "ServerAssignment");

            migrationBuilder.DropTable(
                name: "Proxy");

            migrationBuilder.DropIndex(
                name: "IX_ServerAssignment_ProxyId",
                table: "ServerAssignment");

            migrationBuilder.DropColumn(
                name: "ProxyId",
                table: "ServerAssignment");

            migrationBuilder.DropColumn(
                name: "IsTemporaryServer",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Server");

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerIpAddress",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriggerPort",
                table: "Server",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
