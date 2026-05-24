using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DurationHours = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    TimesheetWeekId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBookings_TimesheetWeeks_TimesheetWeekId",
                        column: x => x.TimesheetWeekId,
                        principalTable: "TimesheetWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBookings_TimesheetWeekId",
                table: "LeaveBookings",
                column: "TimesheetWeekId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveBookings");
        }
    }
}
