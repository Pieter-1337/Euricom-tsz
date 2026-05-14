using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyLeaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLeaves_UserId_LeaveTypeId_Year",
                table: "UserLeaves");

            migrationBuilder.DropIndex(
                name: "IX_LeaveTypes_Name",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "Allowed",
                table: "UserLeaves");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "UserLeaves");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "LeaveTypes");

            migrationBuilder.CreateIndex(
                name: "IX_UserLeaves_UserId_LeaveTypeId",
                table: "UserLeaves",
                columns: new[] { "UserId", "LeaveTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Name",
                table: "LeaveTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLeaves_UserId_LeaveTypeId",
                table: "UserLeaves");

            migrationBuilder.DropIndex(
                name: "IX_LeaveTypes_Name",
                table: "LeaveTypes");

            migrationBuilder.AddColumn<string>(
                name: "Allowed",
                table: "UserLeaves",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "UserLeaves",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "LeaveTypes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLeaves_UserId_LeaveTypeId_Year",
                table: "UserLeaves",
                columns: new[] { "UserId", "LeaveTypeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Name",
                table: "LeaveTypes",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }
    }
}
