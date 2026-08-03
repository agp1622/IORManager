using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicInvoicingEcf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rnc",
                table: "Customers",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EcfRanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RangeStart = table.Column<long>(type: "bigint", nullable: false),
                    RangeEnd = table.Column<long>(type: "bigint", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcfRanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EcfSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ENcf = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnsignedXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TrackId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SecurityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcfSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcfSubmissions_FinancialDocument_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "FinancialDocument",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EcfRanges_DocumentTypeCode",
                table: "EcfRanges",
                column: "DocumentTypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcfSubmissions_ENcf",
                table: "EcfSubmissions",
                column: "ENcf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcfSubmissions_InvoiceId",
                table: "EcfSubmissions",
                column: "InvoiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EcfRanges");

            migrationBuilder.DropTable(
                name: "EcfSubmissions");

            migrationBuilder.DropColumn(
                name: "Rnc",
                table: "Customers");
        }
    }
}
