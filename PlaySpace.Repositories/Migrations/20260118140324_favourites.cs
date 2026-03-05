using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class favourites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFavouriteBusinessProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavouriteBusinessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavouriteBusinessProfiles_BusinessProfiles_BusinessProf~",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFavouriteBusinessProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteBusinessProfiles_BusinessProfileId",
                table: "UserFavouriteBusinessProfiles",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteBusinessProfiles_UserId",
                table: "UserFavouriteBusinessProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteBusinessProfiles_UserId_BusinessProfileId",
                table: "UserFavouriteBusinessProfiles",
                columns: new[] { "UserId", "BusinessProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFavouriteBusinessProfiles");
        }
    }
}
