using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Catalog;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal static class CatalogSeedData
{
    private static readonly DateTime SeedCreatedAt =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void SeedRestaurants(EntityTypeBuilder<Restaurant> builder)
    {
        builder.HasData(
            new
            {
                Id = 1,
                Name = "Cairo Grill",
                Description = "Charcoal grilled meals and sides.",
                ImageUrl = (string?)null,
                IsActive = true,
                CreatedAt = SeedCreatedAt,
                IsDeleted = false
            },
            new
            {
                Id = 2,
                Name = "Nile Pizza",
                Description = "Fresh pizza and baked pasta.",
                ImageUrl = (string?)null,
                IsActive = true,
                CreatedAt = SeedCreatedAt,
                IsDeleted = false
            });

        builder.OwnsOne(restaurant => restaurant.OpeningHours)
            .HasData(
                new
                {
                    RestaurantId = 1,
                    Start = new TimeOnly(10, 0),
                    End = new TimeOnly(23, 0)
                },
                new
                {
                    RestaurantId = 2,
                    Start = new TimeOnly(11, 0),
                    End = new TimeOnly(1, 0)
                });
    }

    public static void SeedProducts(EntityTypeBuilder<Product> builder)
    {
        builder.HasData(
            new
            {
                Id = 101,
                RestaurantId = 1,
                Name = "Mixed Grill Plate",
                Description = "Chicken, kofta, rice, and salad.",
                ImageUrl = (string?)null,
                IsAvailable = true,
                CreatedAt = SeedCreatedAt,
                IsDeleted = false
            },
            new
            {
                Id = 102,
                RestaurantId = 1,
                Name = "Chicken Shawarma",
                Description = "Grilled chicken wrap with garlic sauce.",
                ImageUrl = (string?)null,
                IsAvailable = true,
                CreatedAt = SeedCreatedAt,
                IsDeleted = false
            },
            new
            {
                Id = 201,
                RestaurantId = 2,
                Name = "Margherita Pizza",
                Description = "Tomato, mozzarella, and basil.",
                ImageUrl = (string?)null,
                IsAvailable = true,
                CreatedAt = SeedCreatedAt,
                IsDeleted = false
            },
            new
            {
                Id = 202,
                RestaurantId = 2,
                Name = "Baked Penne",
                Description = "Penne pasta with tomato sauce and cheese.",
                ImageUrl = (string?)null,
                IsAvailable = true,
                CreatedAt = SeedCreatedAt,
                IsDeleted = false
            });

        builder.OwnsOne(product => product.CurrentPrice)
            .HasData(
                new { ProductId = 101, Amount = 185m },
                new { ProductId = 102, Amount = 95m },
                new { ProductId = 201, Amount = 140m },
                new { ProductId = 202, Amount = 125m });
    }
}
