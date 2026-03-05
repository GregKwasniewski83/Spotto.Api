using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class up : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerDateAvailabilities_TrainerBusinessAssociations_Assoc~",
                table: "TrainerDateAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerScheduleTemplates_TrainerBusinessAssociations_Associ~",
                table: "TrainerScheduleTemplates");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerDateAvailabilities_BusinessProfiles_AssociatedBusine~",
                table: "TrainerDateAvailabilities",
                column: "AssociatedBusinessId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerScheduleTemplates_BusinessProfiles_AssociatedBusines~",
                table: "TrainerScheduleTemplates",
                column: "AssociatedBusinessId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerDateAvailabilities_BusinessProfiles_AssociatedBusine~",
                table: "TrainerDateAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerScheduleTemplates_BusinessProfiles_AssociatedBusines~",
                table: "TrainerScheduleTemplates");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerDateAvailabilities_TrainerBusinessAssociations_Assoc~",
                table: "TrainerDateAvailabilities",
                column: "AssociatedBusinessId",
                principalTable: "TrainerBusinessAssociations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerScheduleTemplates_TrainerBusinessAssociations_Associ~",
                table: "TrainerScheduleTemplates",
                column: "AssociatedBusinessId",
                principalTable: "TrainerBusinessAssociations",
                principalColumn: "Id");
        }
    }
}
