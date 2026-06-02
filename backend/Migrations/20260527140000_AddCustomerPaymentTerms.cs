using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPaymentTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultPaymentTermsDays",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultPaymentTermsDays",
                table: "Customers");
        }
    }
}
