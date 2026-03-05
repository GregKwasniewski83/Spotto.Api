using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class productpurchasegross : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossPrice",
                table: "ProductPurchases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "ProductPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossPrice",
                table: "ProductPurchases");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "ProductPurchases");
        }
    }
}
