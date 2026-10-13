using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talabat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryOperationalContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PickupBuildingNumber",
                table: "Restaurants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupCity",
                table: "Restaurants",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupFloor",
                table: "Restaurants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupStreet",
                table: "Restaurants",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantName",
                table: "Deliveries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantPickupBuildingNumber",
                table: "Deliveries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantPickupCity",
                table: "Deliveries",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantPickupFloor",
                table: "Deliveries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantPickupStreet",
                table: "Deliveries",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Restaurants]
                SET [PickupStreet] = N'1 Demo Grill Street',
                    [PickupCity] = N'Cairo',
                    [PickupBuildingNumber] = N'1',
                    [PickupFloor] = N'Ground Floor'
                WHERE [Id] = 1 AND [Name] = N'Cairo Grill';

                UPDATE [Restaurants]
                SET [PickupStreet] = N'2 Demo Pizza Street',
                    [PickupCity] = N'Cairo',
                    [PickupBuildingNumber] = N'2',
                    [PickupFloor] = NULL
                WHERE [Id] = 2 AND [Name] = N'Nile Pizza';

                IF EXISTS
                (
                    SELECT 1
                    FROM [Restaurants]
                    WHERE [PickupStreet] IS NULL
                       OR [PickupCity] IS NULL
                       OR [PickupBuildingNumber] IS NULL
                )
                BEGIN
                    THROW 51000, 'Delivery operational contract migration blocked: one or more non-demo restaurants require a truthful pickup address.', 1;
                END;

                UPDATE delivery
                SET [RestaurantName] = restaurant.[Name],
                    [RestaurantPickupStreet] = restaurant.[PickupStreet],
                    [RestaurantPickupCity] = restaurant.[PickupCity],
                    [RestaurantPickupBuildingNumber] = restaurant.[PickupBuildingNumber],
                    [RestaurantPickupFloor] = restaurant.[PickupFloor]
                FROM [Deliveries] AS delivery
                INNER JOIN [Restaurants] AS restaurant
                    ON restaurant.[Id] = delivery.[RestaurantId];

                IF EXISTS
                (
                    SELECT 1
                    FROM [Deliveries]
                    WHERE [RestaurantName] IS NULL
                       OR [RestaurantPickupStreet] IS NULL
                       OR [RestaurantPickupCity] IS NULL
                       OR [RestaurantPickupBuildingNumber] IS NULL
                )
                BEGIN
                    THROW 51001, 'Delivery operational contract migration blocked: one or more deliveries could not be backfilled from a trustworthy restaurant pickup address.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PickupBuildingNumber",
                table: "Restaurants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PickupCity",
                table: "Restaurants",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PickupStreet",
                table: "Restaurants",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RestaurantName",
                table: "Deliveries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RestaurantPickupBuildingNumber",
                table: "Deliveries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RestaurantPickupCity",
                table: "Deliveries",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RestaurantPickupStreet",
                table: "Deliveries",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupBuildingNumber",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PickupCity",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PickupFloor",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PickupStreet",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "RestaurantName",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "RestaurantPickupBuildingNumber",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "RestaurantPickupCity",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "RestaurantPickupFloor",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "RestaurantPickupStreet",
                table: "Deliveries");
        }
    }
}
