using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class buscolors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssociatedBusinessId",
                table: "TrainerScheduleTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssociatedBusinessId",
                table: "TrainerDateAvailabilities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerScheduleTemplates_AssociatedBusinessId",
                table: "TrainerScheduleTemplates",
                column: "AssociatedBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerDateAvailabilities_AssociatedBusinessId",
                table: "TrainerDateAvailabilities",
                column: "AssociatedBusinessId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerDateAvailabilities_TrainerBusinessAssociations_Assoc~",
                table: "TrainerDateAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerScheduleTemplates_TrainerBusinessAssociations_Associ~",
                table: "TrainerScheduleTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TrainerScheduleTemplates_AssociatedBusinessId",
                table: "TrainerScheduleTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TrainerDateAvailabilities_AssociatedBusinessId",
                table: "TrainerDateAvailabilities");

            migrationBuilder.DropColumn(
                name: "AssociatedBusinessId",
                table: "TrainerScheduleTemplates");

            migrationBuilder.DropColumn(
                name: "AssociatedBusinessId",
                table: "TrainerDateAvailabilities");
        }
    }
}
