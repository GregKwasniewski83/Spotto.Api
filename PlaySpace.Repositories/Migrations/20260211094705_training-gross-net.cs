using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class traininggrossnet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossPrice",
                table: "Trainings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "Trainings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossPrice",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Trainings");
        }
    }
}
