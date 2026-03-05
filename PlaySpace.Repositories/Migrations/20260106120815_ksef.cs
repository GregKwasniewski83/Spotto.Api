using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class ksef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KSeFInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    KSeFReferenceNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SellerNIP = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SellerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SellerAddress = table.Column<string>(type: "text", nullable: false),
                    SellerCity = table.Column<string>(type: "text", nullable: false),
                    SellerPostalCode = table.Column<string>(type: "text", nullable: false),
                    BuyerNIP = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BuyerAddress = table.Column<string>(type: "text", nullable: true),
                    BuyerCity = table.Column<string>(type: "text", nullable: true),
                    BuyerPostalCode = table.Column<string>(type: "text", nullable: true),
                    BuyerEmail = table.Column<string>(type: "text", nullable: true),
                    BuyerPhone = table.Column<string>(type: "text", nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATRate = table.Column<int>(type: "integer", nullable: false),
                    InvoiceItems = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    KSeFStatus = table.Column<string>(type: "text", nullable: true),
                    KSeFErrorMessage = table.Column<string>(type: "text", nullable: true),
                    KSeFSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KSeFAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoiceXML = table.Column<string>(type: "text", nullable: true),
                    InvoiceJSON = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KSeFInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KSeFInvoices_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KSeFInvoices_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KSeFInvoices_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KSeFInvoices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_BusinessProfileId",
                table: "KSeFInvoices",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_CreatedAt",
                table: "KSeFInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_InvoiceNumber",
                table: "KSeFInvoices",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_KSeFReferenceNumber",
                table: "KSeFInvoices",
                column: "KSeFReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_PaymentId",
                table: "KSeFInvoices",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_ReservationId",
                table: "KSeFInvoices",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_Status",
                table: "KSeFInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFInvoices_UserId",
                table: "KSeFInvoices",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KSeFInvoices");
        }
    }
}
