using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalRegimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalRegimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InvoiceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalRegimes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FiscalRegimes",
                columns: new[] { "Id", "Code", "Name", "InvoiceCount" },
                values: new object[,]
                {
                    {  1, "B01", "Factura de Crédito Fiscal",                   0 },
                    {  2, "B02", "Factura de Consumo",                           0 },
                    {  3, "B03", "Notas de Débito",                              0 },
                    {  4, "B04", "Notas de Crédito",                             0 },
                    {  5, "B11", "Comprobante de Compras",                       0 },
                    {  6, "B12", "Registro Único de Ingresos",                   0 },
                    {  7, "B13", "Comprobante para Gastos Menores",              0 },
                    {  8, "B14", "Comprobante para Regímenes Especiales",        0 },
                    {  9, "B15", "Comprobante Gubernamental",                    0 },
                    { 10, "B16", "Comprobante para Exportaciones",               0 },
                    { 11, "B17", "Comprobante para Pagos al Exterior",           0 },
                    { 12, "E31", "e-Crédito Fiscal",                             0 },
                    { 13, "E32", "e-Consumo",                                    0 },
                    { 14, "E33", "e-Notas de Débito",                            0 },
                    { 15, "E34", "e-Notas de Crédito",                           0 },
                    { 16, "E41", "e-Comprobante de Compras",                     0 },
                    { 17, "E43", "e-Comprobante para Gastos Menores",            0 },
                    { 18, "E44", "e-Comprobante para Regímenes Especiales",      0 },
                    { 19, "E45", "e-Comprobante Gubernamental",                  0 },
                    { 20, "E46", "e-Comprobante para Exportaciones",             0 },
                    { 21, "E47", "e-Comprobante para Pagos al Exterior",         0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalRegimes_Code",
                table: "FiscalRegimes",
                column: "Code",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "FiscalRegimeId",
                table: "FinancialDocument",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_FiscalRegimeId",
                table: "FinancialDocument",
                column: "FiscalRegimeId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialDocument_FiscalRegimes_FiscalRegimeId",
                table: "FinancialDocument",
                column: "FiscalRegimeId",
                principalTable: "FiscalRegimes",
                principalColumn: "Id");

            // Backfill: link existing invoices to their fiscal regime by NcfCategory
            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [FiscalRegimeId] = (
                    SELECT [Id] FROM [FiscalRegimes]
                    WHERE [Code] = [FinancialDocument].[NcfCategory]
                )
                WHERE [DocumentType] = 'Invoice'
                  AND [NcfCategory] IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM [FiscalRegimes]
                      WHERE [Code] = [FinancialDocument].[NcfCategory]
                  );
                """);

            // Backfill: sync InvoiceCount to reflect pre-existing invoices
            migrationBuilder.Sql("""
                UPDATE fr
                SET fr.[InvoiceCount] = (
                    SELECT COUNT(*)
                    FROM [FinancialDocument] fd
                    WHERE fd.[DocumentType] = 'Invoice'
                      AND fd.[FiscalRegimeId] = fr.[Id]
                )
                FROM [FiscalRegimes] fr;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialDocument_FiscalRegimes_FiscalRegimeId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_FiscalRegimeId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "FiscalRegimeId",
                table: "FinancialDocument");

            migrationBuilder.DropTable(
                name: "FiscalRegimes");
        }
    }
}
