using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeavesModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvDays",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AncienniteitDays",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HolidayDays",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SicknessDays",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DefaultDays = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    DefaultAllowed = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLeaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalDays = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLeaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLeaves_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserLeaves_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "LeaveTypes",
                columns: new[] { "Id", "DefaultAllowed", "DefaultDays", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-000000000001"), "Limited", 20m, "Verlof" },
                    { new Guid("11111111-1111-1111-1111-000000000002"), "Limited", 5m, "ADV dagen" },
                    { new Guid("11111111-1111-1111-1111-000000000003"), "Limited", 0m, "Anciënniteit" },
                    { new Guid("11111111-1111-1111-1111-000000000004"), "Unlimited", null, "Ziekte" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Name",
                table: "LeaveTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLeaves_LeaveTypeId",
                table: "UserLeaves",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLeaves_UserId_LeaveTypeId_Year",
                table: "UserLeaves",
                columns: new[] { "UserId", "LeaveTypeId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLeaves");

            migrationBuilder.DropTable(
                name: "LeaveTypes");

            migrationBuilder.AddColumn<decimal>(
                name: "AdvDays",
                table: "Users",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AncienniteitDays",
                table: "Users",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HolidayDays",
                table: "Users",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SicknessDays",
                table: "Users",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
