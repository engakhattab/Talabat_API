using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(
            "Orders",
            table => table.HasCheckConstraint(
                "CK_Orders_TotalAmount_NonNegative",
                "[TotalAmount] >= 0"));

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();
        builder.Ignore(order => order.Items);

        builder.Property(order => order.CustomerId)
            .IsRequired();

        builder.Property(order => order.RestaurantId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(order => order.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(
            order => order.TotalAmount,
            total => total.ConfigureMoney("TotalAmount"));

        builder.OwnsOne(
            order => order.DeliveryAddress,
            deliveryAddress => deliveryAddress.ConfigureDeliveryAddressSnapshot());

        builder.HasIndex(order => new { order.CustomerId, order.CreatedAt })
            .HasDatabaseName("IX_Orders_CustomerId_CreatedAt");

        builder.OwnsMany<OrderItem>(
            "_items",
            item =>
            {
                item.ToTable(
                    "OrderItems",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_OrderItems_Quantity_Positive",
                            "[Quantity] > 0");
                        table.HasCheckConstraint(
                            "CK_OrderItems_UnitPriceAmount_NonNegative",
                            "[UnitPriceAmount] >= 0");
                        table.HasCheckConstraint(
                            "CK_OrderItems_LineTotalAmount_NonNegative",
                            "[LineTotalAmount] >= 0");
                    });

                item.WithOwner().HasForeignKey("OrderId");
                item.HasKey("OrderId", nameof(OrderItem.ProductId));

                item.Property(orderItem => orderItem.ProductId)
                    .IsRequired();

                item.Property(orderItem => orderItem.ProductName)
                    .HasMaxLength(200)
                    .IsRequired();

                item.Property(orderItem => orderItem.Quantity)
                    .IsRequired();

                item.OwnsOne(
                    orderItem => orderItem.UnitPrice,
                    unitPrice => unitPrice.ConfigureMoney("UnitPriceAmount"));

                item.OwnsOne(
                    orderItem => orderItem.LineTotal,
                    lineTotal => lineTotal.ConfigureMoney("LineTotalAmount"));

                item.HasOne<Product>()
                    .WithMany()
                    .HasForeignKey(orderItem => orderItem.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        builder.Navigation("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
