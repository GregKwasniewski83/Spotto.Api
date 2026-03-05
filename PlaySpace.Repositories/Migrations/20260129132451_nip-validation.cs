using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class nipvalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessProfiles_Nip",
                table: "BusinessProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_Nip",
                table: "BusinessProfiles",
                column: "Nip");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessProfiles_Nip",
                table: "BusinessProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_Nip",
                table: "BusinessProfiles",
                column: "Nip",
                unique: true);
        }
    }
}
