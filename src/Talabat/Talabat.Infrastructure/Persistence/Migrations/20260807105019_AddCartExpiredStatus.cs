using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talabat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartExpiredStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Carts_Status",
                table: "Carts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Carts_Status",
                table: "Carts",
                sql: "[Status] IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Carts_Status",
                table: "Carts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Carts_Status",
                table: "Carts",
                sql: "[Status] IN (1, 2, 3)");
        }
    }
}
