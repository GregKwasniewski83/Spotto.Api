using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class products2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_BusinessProfiles_BusinessProfileId1",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessProfileId1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId1",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId1",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessProfileId1",
                table: "Products",
                column: "BusinessProfileId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_BusinessProfiles_BusinessProfileId1",
                table: "Products",
                column: "BusinessProfileId1",
                principalTable: "BusinessProfiles",
                principalColumn: "Id");
        }
    }
}
