using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimesheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimesheetWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsoYear = table.Column<int>(type: "INTEGER", nullable: false),
                    IsoWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetWeeks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DurationHours = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    TimesheetWeekId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntries_TimesheetWeeks_TimesheetWeekId",
                        column: x => x.TimesheetWeekId,
                        principalTable: "TimesheetWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_TimesheetWeekId",
                table: "TimeEntries",
                column: "TimesheetWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetWeeks_UserId_IsoYear_IsoWeek",
                table: "TimesheetWeeks",
                columns: new[] { "UserId", "IsoYear", "IsoWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeEntries");

            migrationBuilder.DropTable(
                name: "TimesheetWeeks");
        }
    }
}
