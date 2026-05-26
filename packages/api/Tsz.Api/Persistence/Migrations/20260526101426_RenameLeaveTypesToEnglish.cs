using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameLeaveTypesToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000001"),
                column: "Name",
                value: "Annual leave");

            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000002"),
                column: "Name",
                value: "ADV days");

            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000003"),
                column: "Name",
                value: "Seniority leave");

            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000004"),
                column: "Name",
                value: "Sick leave");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000001"),
                column: "Name",
                value: "Verlof");

            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000002"),
                column: "Name",
                value: "ADV dagen");

            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000003"),
                column: "Name",
                value: "Anciënniteit");

            migrationBuilder.UpdateData(
                table: "LeaveTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000004"),
                column: "Name",
                value: "Ziekte");
        }
    }
}
