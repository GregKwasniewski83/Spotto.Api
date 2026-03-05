using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class pendingreservationupdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfUsers",
                table: "PendingTimeSlotReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PayForAllUsers",
                table: "PendingTimeSlotReservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "PendingTimeSlotReservations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfUsers",
                table: "PendingTimeSlotReservations");

            migrationBuilder.DropColumn(
                name: "PayForAllUsers",
                table: "PendingTimeSlotReservations");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "PendingTimeSlotReservations");
        }
    }
}
