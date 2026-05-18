using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteCustomerPOAndPurchaseOrderQuoteLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPONumber",
                table: "FinancialDocument",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrder_QuoteId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuoteAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteAttachments_FinancialDocument_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_PurchaseOrder_QuoteId",
                table: "FinancialDocument",
                column: "PurchaseOrder_QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteAttachments_QuoteId",
                table: "QuoteAttachments",
                column: "QuoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialDocument_FinancialDocument_PurchaseOrder_QuoteId",
                table: "FinancialDocument",
                column: "PurchaseOrder_QuoteId",
                principalTable: "FinancialDocument",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialDocument_FinancialDocument_PurchaseOrder_QuoteId",
                table: "FinancialDocument");

            migrationBuilder.DropTable(
                name: "QuoteAttachments");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_PurchaseOrder_QuoteId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "CustomerPONumber",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "PurchaseOrder_QuoteId",
                table: "FinancialDocument");
        }
    }
}
