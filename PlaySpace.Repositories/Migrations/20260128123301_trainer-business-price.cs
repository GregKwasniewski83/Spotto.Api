using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class trainerbusinessprice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossHourlyRate",
                table: "TrainerBusinessAssociations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "TrainerBusinessAssociations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "TrainerBusinessAssociations",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossHourlyRate",
                table: "TrainerBusinessAssociations");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "TrainerBusinessAssociations");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "TrainerBusinessAssociations");
        }
    }
}
