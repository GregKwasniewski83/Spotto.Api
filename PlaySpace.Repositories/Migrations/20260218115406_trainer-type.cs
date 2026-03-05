using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class trainertype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainerType",
                table: "TrainerProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainerType",
                table: "TrainerProfiles");
        }
    }
}
