using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable(
            "Deliveries",
            table => table.HasCheckConstraint(
                "CK_Deliveries_Status",
                "[Status] IN (1, 2, 3, 4, 5, 6, 7, 8)"));

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();

        builder.Property(delivery => delivery.OrderId)
            .IsRequired();

        builder.Property(delivery => delivery.CustomerId)
            .IsRequired();

        builder.Property(delivery => delivery.RestaurantId)
            .IsRequired();

        builder.Property(delivery => delivery.AssignedAgentId);

        builder.Property(delivery => delivery.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(delivery => delivery.AssignedAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.ArrivedAtRestaurantAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.PickedUpAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.OutForDeliveryAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.DeliveredAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.CancelledAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.FailedAt)
            .HasColumnType("datetime2");

        builder.Property(delivery => delivery.FailureReason)
            .HasMaxLength(1000);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(delivery => delivery.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(delivery => delivery.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(delivery => delivery.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(delivery => delivery.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(
            delivery => delivery.DeliveryAddress,
            deliveryAddress => deliveryAddress.ConfigureDeliveryAddressSnapshot());

        builder.HasIndex(delivery => delivery.OrderId)
            .IsUnique()
            .HasDatabaseName("UX_Deliveries_OrderId");

        builder.HasIndex(delivery => delivery.AssignedAgentId)
            .IsUnique()
            .HasFilter("[AssignedAgentId] IS NOT NULL AND [Status] IN (2, 3, 4, 5) AND [IsDeleted] = CAST(0 AS bit)")
            .HasDatabaseName("UX_Deliveries_AssignedAgentId_Active");
    }
}
