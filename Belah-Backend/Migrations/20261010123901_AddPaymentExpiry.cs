using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Belah_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Orders");
        }
    }
}
