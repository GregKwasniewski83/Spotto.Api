using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class fac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacilityPlanFileName",
                table: "BusinessProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacilityPlanFileType",
                table: "BusinessProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacilityPlanUrl",
                table: "BusinessProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacilityPlanFileName",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "FacilityPlanFileType",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "FacilityPlanUrl",
                table: "BusinessProfiles");
        }
    }
}
