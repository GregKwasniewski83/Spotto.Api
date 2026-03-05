using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class productDTOs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductDetails",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalUsage = table.Column<int>(type: "integer", nullable: false),
                    RemainingUsage = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProductSubtitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProductDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProductPeriod = table.Column<int>(type: "integer", nullable: false),
                    ProductNumOfPeriods = table.Column<int>(type: "integer", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPurchases_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPurchases_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductUsageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductPurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductUsageLogs_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductUsageLogs_ProductPurchases_ProductPurchaseId",
                        column: x => x.ProductPurchaseId,
                        principalTable: "ProductPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductUsageLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPurchases_PaymentId",
                table: "ProductPurchases",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPurchases_ProductId",
                table: "ProductPurchases",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPurchases_PurchaseDate",
                table: "ProductPurchases",
                column: "PurchaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPurchases_UserId",
                table: "ProductPurchases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPurchases_UserId_Status_ExpiryDate",
                table: "ProductPurchases",
                columns: new[] { "UserId", "Status", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUsageLogs_FacilityId",
                table: "ProductUsageLogs",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUsageLogs_ProductPurchaseId",
                table: "ProductUsageLogs",
                column: "ProductPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUsageLogs_UsageDate",
                table: "ProductUsageLogs",
                column: "UsageDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUsageLogs_UserId",
                table: "ProductUsageLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductUsageLogs");

            migrationBuilder.DropTable(
                name: "ProductPurchases");

            migrationBuilder.DropColumn(
                name: "ProductDetails",
                table: "Payments");
        }
    }
}
