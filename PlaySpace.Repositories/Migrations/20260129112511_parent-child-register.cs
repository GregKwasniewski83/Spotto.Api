using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class parentchildregister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessParentChildAssociations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildBusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentBusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfirmationToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConfirmationTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UseParentTPay = table.Column<bool>(type: "boolean", nullable: false),
                    UseParentNipForInvoices = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessParentChildAssociations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessParentChildAssociations_BusinessProfiles_ChildBusin~",
                        column: x => x.ChildBusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessParentChildAssociations_BusinessProfiles_ParentBusi~",
                        column: x => x.ParentBusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessParentChildAssociations_ChildBusinessProfileId",
                table: "BusinessParentChildAssociations",
                column: "ChildBusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessParentChildAssociations_ChildBusinessProfileId_Pare~",
                table: "BusinessParentChildAssociations",
                columns: new[] { "ChildBusinessProfileId", "ParentBusinessProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessParentChildAssociations_ConfirmationToken",
                table: "BusinessParentChildAssociations",
                column: "ConfirmationToken");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessParentChildAssociations_ParentBusinessProfileId",
                table: "BusinessParentChildAssociations",
                column: "ParentBusinessProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessParentChildAssociations");
        }
    }
}
