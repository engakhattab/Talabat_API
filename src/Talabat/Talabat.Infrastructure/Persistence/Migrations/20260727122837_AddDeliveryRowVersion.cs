using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talabat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Deliveries",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Deliveries");
        }
    }
}
