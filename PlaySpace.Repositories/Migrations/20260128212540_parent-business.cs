using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class parentbusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentBusinessProfileId",
                table: "BusinessProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseParentNipForInvoices",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseParentTPay",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_ParentBusinessProfileId",
                table: "BusinessProfiles",
                column: "ParentBusinessProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessProfiles_BusinessProfiles_ParentBusinessProfileId",
                table: "BusinessProfiles",
                column: "ParentBusinessProfileId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessProfiles_BusinessProfiles_ParentBusinessProfileId",
                table: "BusinessProfiles");

            migrationBuilder.DropIndex(
                name: "IX_BusinessProfiles_ParentBusinessProfileId",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "ParentBusinessProfileId",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "UseParentNipForInvoices",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "UseParentTPay",
                table: "BusinessProfiles");
        }
    }
}
