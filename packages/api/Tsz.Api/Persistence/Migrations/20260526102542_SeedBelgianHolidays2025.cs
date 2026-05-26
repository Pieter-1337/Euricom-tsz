using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedBelgianHolidays2025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "Country", "Date", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 1, 1), "New Year's Day", 0 },
                    { new Guid("a0000002-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 4, 21), "Easter Monday", 0 },
                    { new Guid("a0000003-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 5, 1), "Labour Day", 0 },
                    { new Guid("a0000004-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 5, 29), "Ascension Day", 0 },
                    { new Guid("a0000005-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 6, 9), "Whit Monday", 0 },
                    { new Guid("a0000006-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 7, 21), "Belgian National Day", 0 },
                    { new Guid("a0000007-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 8, 15), "Assumption of Mary", 0 },
                    { new Guid("a0000008-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 11, 1), "All Saints' Day", 0 },
                    { new Guid("a0000009-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 11, 11), "Armistice Day", 0 },
                    { new Guid("a0000010-0000-0000-0000-000000000000"), "BE", new DateOnly(2025, 12, 25), "Christmas Day", 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000006-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000008-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: new Guid("a0000010-0000-0000-0000-000000000000"));
        }
    }
}
