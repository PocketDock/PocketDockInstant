using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PocketDockUI.Data.Migrations
{
    public partial class AddSelectedVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedVersion",
                table: "ServerAssignment",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedVersion",
                table: "ServerAssignment");
        }
    }
}
