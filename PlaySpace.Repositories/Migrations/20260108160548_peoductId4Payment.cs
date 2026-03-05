using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class peoductId4Payment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductPurchaseId",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ProductPurchaseId",
                table: "Reservations",
                column: "ProductPurchaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_ProductPurchases_ProductPurchaseId",
                table: "Reservations",
                column: "ProductPurchaseId",
                principalTable: "ProductPurchases",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_ProductPurchases_ProductPurchaseId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_ProductPurchaseId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ProductPurchaseId",
                table: "Reservations");
        }
    }
}
