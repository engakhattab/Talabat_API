using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryAgentConfiguration : IEntityTypeConfiguration<DeliveryAgent>
{
    public void Configure(EntityTypeBuilder<DeliveryAgent> builder)
    {
        builder.ToTable(
            "DeliveryAgents",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_DeliveryAgents_VehicleType",
                    "[VehicleType] IN (1, 2, 3)");
                table.HasCheckConstraint(
                    "CK_DeliveryAgents_Status",
                    "[Status] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_DeliveryAgents_CurrentLocation_PairedNull",
                    "(([CurrentLatitude] IS NULL AND [CurrentLongitude] IS NULL) OR ([CurrentLatitude] IS NOT NULL AND [CurrentLongitude] IS NOT NULL))");
                table.HasCheckConstraint(
                    "CK_DeliveryAgents_CurrentLatitude_Range",
                    "([CurrentLatitude] IS NULL OR ([CurrentLatitude] >= -90 AND [CurrentLatitude] <= 90))");
                table.HasCheckConstraint(
                    "CK_DeliveryAgents_CurrentLongitude_Range",
                    "([CurrentLongitude] IS NULL OR ([CurrentLongitude] >= -180 AND [CurrentLongitude] <= 180))");
            });

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();

        builder.Property(agent => agent.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(agent => agent.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(agent => agent.VehicleType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(agent => agent.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.OwnsOne(
            agent => agent.CurrentLocation,
            location => location.ConfigureGeoLocation());
    }
}
