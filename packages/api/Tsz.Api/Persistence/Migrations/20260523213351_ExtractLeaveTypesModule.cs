using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtractLeaveTypesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserLeaves_Users_UserId",
                table: "UserLeaves");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_UserLeaves_Users_UserId",
                table: "UserLeaves",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
