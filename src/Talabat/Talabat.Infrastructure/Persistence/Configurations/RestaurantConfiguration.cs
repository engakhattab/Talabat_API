using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Catalog;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class RestaurantConfiguration :
    IEntityTypeConfiguration<Restaurant>,
    IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");
        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();
        builder.Ignore(restaurant => restaurant.Products);

        builder.Property(restaurant => restaurant.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(restaurant => restaurant.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(restaurant => restaurant.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(restaurant => restaurant.IsActive)
            .IsRequired();

        builder.OwnsOne(
            restaurant => restaurant.OpeningHours,
            openingHours => openingHours.ConfigureTimeRange());

        builder.HasMany<Product>("_products")
            .WithOne()
            .HasForeignKey(product => product.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_products")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(restaurant => restaurant.IsActive)
            .HasDatabaseName("IX_Restaurants_IsActive");

        CatalogSeedData.SeedRestaurants(builder);
    }

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "Products",
            table => table.HasCheckConstraint(
                "CK_Products_CurrentPriceAmount_NonNegative",
                "[CurrentPriceAmount] >= 0"));

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();

        builder.Property(product => product.RestaurantId)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(product => product.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(product => product.IsAvailable)
            .IsRequired();

        builder.OwnsOne(
            product => product.CurrentPrice,
            currentPrice => currentPrice.ConfigureMoney("CurrentPriceAmount"));

        builder.HasIndex(product => new { product.RestaurantId, product.Name })
            .IsUnique()
            .HasDatabaseName("UX_Products_RestaurantId_Name");

        CatalogSeedData.SeedProducts(builder);
    }
}
