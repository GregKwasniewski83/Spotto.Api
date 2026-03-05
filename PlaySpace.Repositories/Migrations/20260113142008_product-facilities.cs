using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class productfacilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliesToAllFacilities",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FacilityIds",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppliesToAllFacilities",
                table: "ProductPurchases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "ProductPurchases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FacilityIds",
                table: "ProductPurchases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesToAllFacilities",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FacilityIds",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AppliesToAllFacilities",
                table: "ProductPurchases");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "ProductPurchases");

            migrationBuilder.DropColumn(
                name: "FacilityIds",
                table: "ProductPurchases");
        }
    }
}
