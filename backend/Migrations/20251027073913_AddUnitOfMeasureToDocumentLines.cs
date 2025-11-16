using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitOfMeasureToDocumentLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                table: "DocumentLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "unit");

            migrationBuilder.Sql("UPDATE DocumentLines SET UnitOfMeasure = 'unit' WHERE UnitOfMeasure = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "DocumentLines");
        }
    }
}
