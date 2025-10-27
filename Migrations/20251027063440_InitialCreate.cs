using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(21)", maxLength: 21, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Receipt_CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialDocument", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLines_FinancialDocument_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentLines_FinancialDocument_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReceiptPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Method = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptPayments_FinancialDocument_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_InvoiceId",
                table: "DocumentLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_PurchaseOrderId",
                table: "DocumentLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPayments_ReceiptId",
                table: "ReceiptPayments",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentLines");

            migrationBuilder.DropTable(
                name: "ReceiptPayments");

            migrationBuilder.DropTable(
                name: "FinancialDocument");
        }
    }
}
