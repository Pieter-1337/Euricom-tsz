using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.Role });
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill: copy each user's existing single Role into the new junction table.
            migrationBuilder.Sql(
                "INSERT INTO UserRoles (UserId, Role) SELECT Id, Role FROM Users WHERE Role IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "User");

            // Restore the first role per user back into Users.Role.
            migrationBuilder.Sql(
                "UPDATE Users SET Role = (SELECT Role FROM UserRoles WHERE UserRoles.UserId = Users.Id LIMIT 1);");

            migrationBuilder.DropTable(
                name: "UserRoles");
        }
    }
}
