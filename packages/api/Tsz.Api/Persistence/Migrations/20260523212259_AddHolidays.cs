using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "Country", "Date", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("a1000001-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 1, 1), "New Year's Day", 0 },
                    { new Guid("a1000002-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 4, 6), "Easter Monday", 0 },
                    { new Guid("a1000003-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 5, 1), "Labour Day", 0 },
                    { new Guid("a1000004-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 5, 14), "Ascension Day", 0 },
                    { new Guid("a1000005-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 5, 25), "Whit Monday", 0 },
                    { new Guid("a1000006-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 7, 21), "Belgian National Day", 0 },
                    { new Guid("a1000007-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 8, 15), "Assumption of Mary", 0 },
                    { new Guid("a1000008-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 11, 1), "All Saints' Day", 0 },
                    { new Guid("a1000009-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 11, 11), "Armistice Day", 0 },
                    { new Guid("a1000010-0000-0000-0000-000000000000"), "BE", new DateOnly(2026, 12, 25), "Christmas Day", 0 },
                    { new Guid("a2000001-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 1, 1), "New Year's Day", 0 },
                    { new Guid("a2000002-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 3, 29), "Easter Monday", 0 },
                    { new Guid("a2000003-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 5, 1), "Labour Day", 0 },
                    { new Guid("a2000004-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 5, 6), "Ascension Day", 0 },
                    { new Guid("a2000005-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 5, 17), "Whit Monday", 0 },
                    { new Guid("a2000006-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 7, 21), "Belgian National Day", 0 },
                    { new Guid("a2000007-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 8, 15), "Assumption of Mary", 0 },
                    { new Guid("a2000008-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 11, 1), "All Saints' Day", 0 },
                    { new Guid("a2000009-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 11, 11), "Armistice Day", 0 },
                    { new Guid("a2000010-0000-0000-0000-000000000000"), "BE", new DateOnly(2027, 12, 25), "Christmas Day", 0 },
                    { new Guid("a3000001-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 1, 1), "New Year's Day", 0 },
                    { new Guid("a3000002-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 4, 17), "Easter Monday", 0 },
                    { new Guid("a3000003-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 5, 1), "Labour Day", 0 },
                    { new Guid("a3000004-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 5, 25), "Ascension Day", 0 },
                    { new Guid("a3000005-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 6, 5), "Whit Monday", 0 },
                    { new Guid("a3000006-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 7, 21), "Belgian National Day", 0 },
                    { new Guid("a3000007-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 8, 15), "Assumption of Mary", 0 },
                    { new Guid("a3000008-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 11, 1), "All Saints' Day", 0 },
                    { new Guid("a3000009-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 11, 11), "Armistice Day", 0 },
                    { new Guid("a3000010-0000-0000-0000-000000000000"), "BE", new DateOnly(2028, 12, 25), "Christmas Day", 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Country_Date",
                table: "Holidays",
                columns: new[] { "Country", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holidays");
        }
    }
}
