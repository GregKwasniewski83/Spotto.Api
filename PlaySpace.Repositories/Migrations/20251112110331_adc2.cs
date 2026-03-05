using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class adc2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "BusinessProfiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "BusinessProfiles",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "BusinessProfiles");
        }
    }
}
