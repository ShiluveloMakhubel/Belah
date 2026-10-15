using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Belah_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueApprovalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPriceApproved",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockConfirmed",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPriceApproved",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsStockConfirmed",
                table: "Products");
        }
    }
}
