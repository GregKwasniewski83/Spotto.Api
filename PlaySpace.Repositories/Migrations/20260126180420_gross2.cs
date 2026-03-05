using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class gross2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfUsers",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PaidForAllUsers",
                table: "Reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfUsers",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PaidForAllUsers",
                table: "Reservations");
        }
    }
}
