using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address_City",
                table: "Customers",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Country",
                table: "Customers",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Street",
                table: "Customers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Zip",
                table: "Customers",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson_Email",
                table: "Customers",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson_Name",
                table: "Customers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Number",
                table: "Customers",
                column: "Number",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_Number",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address_City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address_Country",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address_Street",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address_Zip",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactPerson_Email",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactPerson_Name",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Customers");
        }
    }
}
