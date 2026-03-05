using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddKSeFFieldsToBusinessProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KSeFEnabled",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KSeFToken",
                table: "BusinessProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KSeFEnvironment",
                table: "BusinessProfiles",
                type: "text",
                nullable: false,
                defaultValue: "Test");

            migrationBuilder.AddColumn<DateTime>(
                name: "KSeFRegisteredAt",
                table: "BusinessProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KSeFLastSyncAt",
                table: "BusinessProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KSeFEnabled",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KSeFToken",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KSeFEnvironment",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KSeFRegisteredAt",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KSeFLastSyncAt",
                table: "BusinessProfiles");
        }
    }
}
