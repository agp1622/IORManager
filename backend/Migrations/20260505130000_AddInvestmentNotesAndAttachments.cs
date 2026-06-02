using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentNotesAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Investment notes on the shared FinancialDocument table (TPH — only populated for PurchaseOrder rows)
            migrationBuilder.AddColumn<string>(
                name: "InvestmentNotes",
                table: "FinancialDocument",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrderAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderAttachments_FinancialDocument_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderAttachments_PurchaseOrderId",
                table: "PurchaseOrderAttachments",
                column: "PurchaseOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PurchaseOrderAttachments");

            migrationBuilder.DropColumn(
                name: "InvestmentNotes",
                table: "FinancialDocument");
        }
    }
}
