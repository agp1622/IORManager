using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddNcfCategoriesAndPerCategorySequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "NextNumber",
                table: "NcfSequences",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CategoryCode",
                table: "NcfSequences",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NcfCategory",
                table: "FinancialDocument",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "NcfSequences",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CategoryCode", "NextNumber" },
                values: new object[] { "B02", 1L });

            migrationBuilder.Sql("""
                UPDATE [NcfSequences]
                SET [CategoryCode] = 'B02'
                WHERE [CategoryCode] = '';
                """);

            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [NcfCategory] = UPPER(LEFT(REPLACE(REPLACE([NcfNumber], '-', ''), ' ', ''), 3))
                WHERE [DocumentType] = 'Invoice'
                  AND [NcfNumber] IS NOT NULL
                  AND REPLACE(REPLACE([NcfNumber], '-', ''), ' ', '') LIKE '[BE][0-9][0-9]%';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_NcfSequences_CategoryCode",
                table: "NcfSequences",
                column: "CategoryCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NcfSequences_CategoryCode",
                table: "NcfSequences");

            migrationBuilder.DropColumn(
                name: "CategoryCode",
                table: "NcfSequences");

            migrationBuilder.DropColumn(
                name: "NcfCategory",
                table: "FinancialDocument");

            migrationBuilder.AlterColumn<int>(
                name: "NextNumber",
                table: "NcfSequences",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.UpdateData(
                table: "NcfSequences",
                keyColumn: "Id",
                keyValue: 1,
                column: "NextNumber",
                value: 1);
        }
    }
}
