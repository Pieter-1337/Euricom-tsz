using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsz.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerClientManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientManagerId",
                table: "Customers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientManagerId",
                table: "Customers");
        }
    }
}
