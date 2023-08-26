using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PocketDockUI.Data.Migrations
{
    public partial class RenameToTriggerIp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PublicIpAddress",
                table: "Server",
                newName: "TriggerIpAddress");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TriggerIpAddress",
                table: "Server",
                newName: "PublicIpAddress");
        }
    }
}
