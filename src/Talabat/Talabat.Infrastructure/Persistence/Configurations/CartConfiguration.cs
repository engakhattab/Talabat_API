using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Catalog;
using CustomerAggregate = Talabat.Domain.Aggregates.Customer.Customer;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable(
            "Carts",
            table => table.HasCheckConstraint("CK_Carts_Status", "[Status] IN (1, 2, 3)"));

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();
        builder.Ignore(cart => cart.Items);

        builder.Property(cart => cart.CustomerId)
            .IsRequired();

        builder.Property(cart => cart.RestaurantId)
            .IsRequired();

        builder.Property(cart => cart.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne<CustomerAggregate>()
            .WithMany()
            .HasForeignKey(cart => cart.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(cart => cart.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cart => cart.CustomerId)
            .IsUnique()
            .HasFilter("[Status] = 1 AND [IsDeleted] = CAST(0 AS bit)")
            .HasDatabaseName("UX_Carts_CustomerId_Active");

        builder.OwnsMany<CartItem>(
            "_items",
            item =>
            {
                item.ToTable(
                    "CartItems",
                    table => table.HasCheckConstraint(
                        "CK_CartItems_Quantity_Positive",
                        "[Quantity] > 0"));

                item.WithOwner().HasForeignKey("CartId");
                item.HasKey("CartId", nameof(CartItem.ProductId));

                item.Property(cartItem => cartItem.ProductId)
                    .IsRequired();

                item.Property(cartItem => cartItem.ProductName)
                    .HasMaxLength(200)
                    .IsRequired();

                item.Property(cartItem => cartItem.Quantity)
                    .IsRequired();

                item.HasOne<Product>()
                    .WithMany()
                    .HasForeignKey(cartItem => cartItem.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        builder.Navigation("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
