using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class OrderFulfillmentConfiguration : IEntityTypeConfiguration<OrderFulfillment>
{
    public void Configure(EntityTypeBuilder<OrderFulfillment> builder)
    {
        builder.ToTable(
            "OrderFulfillments",
            table => table.HasCheckConstraint(
                "CK_OrderFulfillments_Status",
                "[Status] IN (1, 2, 3, 4, 5, 6, 7)"));

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();

        builder.Property(fulfillment => fulfillment.OrderId)
            .IsRequired();

        builder.Property(fulfillment => fulfillment.RestaurantId)
            .IsRequired();

        builder.Property(fulfillment => fulfillment.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(fulfillment => fulfillment.PlacedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(fulfillment => fulfillment.AcceptanceDeadline)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(fulfillment => fulfillment.AcceptedAt)
            .HasColumnType("datetime2");

        builder.Property(fulfillment => fulfillment.RowVersion)
            .IsRowVersion();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(fulfillment => fulfillment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(fulfillment => fulfillment.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(fulfillment => fulfillment.OrderId)
            .IsUnique()
            .HasDatabaseName("UX_OrderFulfillments_OrderId");
    }
}
