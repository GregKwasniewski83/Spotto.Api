using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class maxusers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossPricePerHour",
                table: "Facilities",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PricePerUser",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossPricePerHour",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "PricePerUser",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Facilities");
        }
    }
}
