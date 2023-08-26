#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace PocketDockUI.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerAssignment",
                columns: table => new
                {
                    AssignedUserId = table.Column<string>(type: "text", nullable: false),
                    GameServerPort = table.Column<int>(type: "integer", nullable: true),
                    AssignedUserIpAddress = table.Column<string>(type: "text", nullable: true),
                    StartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivity = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ServerPass = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerAssignment", x => x.AssignedUserId);
                });

            migrationBuilder.CreateTable(
                name: "Server",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrivateIpAddress = table.Column<string>(type: "text", nullable: true),
                    PublicIpAddress = table.Column<string>(type: "text", nullable: true),
                    Domain = table.Column<string>(type: "text", nullable: true),
                    ServerId = table.Column<string>(type: "text", nullable: true),
                    TriggerPort = table.Column<int>(type: "integer", nullable: false),
                    GameServerPortRangeStart = table.Column<int>(type: "integer", nullable: false),
                    GameServerPortRangeEnd = table.Column<int>(type: "integer", nullable: false),
                    ServerAssignmentId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Server", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Server_ServerAssignment_ServerAssignmentId",
                        column: x => x.ServerAssignmentId,
                        principalTable: "ServerAssignment",
                        principalColumn: "AssignedUserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Server_ServerAssignmentId",
                table: "Server",
                column: "ServerAssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Server");

            migrationBuilder.DropTable(
                name: "ServerAssignment");
        }
    }
}
