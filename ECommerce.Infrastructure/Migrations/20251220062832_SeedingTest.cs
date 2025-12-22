using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedingTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Inventory",
                columns: new[] { "Sku", "ActualQty", "ReservedQty" },
                values: new object[,]
                {
                    { "SKU-IPHONE-15", 100, 0 },
                    { "SKU-SAMSUNG-S24", 50, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Inventory",
                keyColumn: "Sku",
                keyValue: "SKU-IPHONE-15");

            migrationBuilder.DeleteData(
                table: "Inventory",
                keyColumn: "Sku",
                keyValue: "SKU-SAMSUNG-S24");
        }
    }
}
