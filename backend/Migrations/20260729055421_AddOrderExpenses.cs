using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    HasInvoice = table.Column<bool>(type: "bit", nullable: false),
                    Rnc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceiptFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceiptContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiptFileSize = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptFileData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ReceiptUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderExpenses_FinancialDocument_OrderId",
                        column: x => x.OrderId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderExpenses_OrderId",
                table: "OrderExpenses",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderExpenses");
        }
    }
}
