using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("AspNetUsers");

        builder.Property(user => user.FullName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Age);
        builder.Property(user => user.UserType).HasConversion<int>().IsRequired();
        builder.Property(user => user.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(user => user.VehicleType).HasConversion<int?>();
        builder.Property(user => user.DeliveryAgentStatus).HasConversion<int?>();
        builder.Property(user => user.AgentApprovalStatus).HasConversion<int?>();
        builder.Property(user => user.RowVersion).IsRowVersion();

        builder.ConfigureAuditing<User>();

        // GeoLocation
        builder.OwnsOne(user => user.CurrentLocation, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("CurrentLatitude").HasPrecision(9, 6);
            location.Property(l => l.Longitude).HasColumnName("CurrentLongitude").HasPrecision(9, 6);
        });

        // CHECK constraints
        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("CK_Users_Age", "([Age] IS NULL OR [Age] > 0)");
            table.HasCheckConstraint("CK_Users_VehicleType", "([VehicleType] IS NULL OR [VehicleType] IN (1, 2, 3))");
            table.HasCheckConstraint("CK_Users_DeliveryAgentStatus", "([DeliveryAgentStatus] IS NULL OR [DeliveryAgentStatus] IN (1, 2, 3, 4))");
            table.HasCheckConstraint("CK_Users_AgentApprovalStatus", "([AgentApprovalStatus] IS NULL OR [AgentApprovalStatus] IN (1, 2, 3))");
            table.HasCheckConstraint("CK_Users_UserType_Range", "([UserType] >= 0 AND [UserType] <= 15)");
            table.HasCheckConstraint("CK_Users_CurrentLocation_PairedNull",
                "(([CurrentLatitude] IS NULL AND [CurrentLongitude] IS NULL) OR ([CurrentLatitude] IS NOT NULL AND [CurrentLongitude] IS NOT NULL))");
            table.HasCheckConstraint("CK_Users_CurrentLatitude_Range",
                "([CurrentLatitude] IS NULL OR ([CurrentLatitude] >= -90 AND [CurrentLatitude] <= 90))");
            table.HasCheckConstraint("CK_Users_CurrentLongitude_Range",
                "([CurrentLongitude] IS NULL OR ([CurrentLongitude] >= -180 AND [CurrentLongitude] <= 180))");
        });

        // Owned addresses
        builder.Ignore(user => user.Addresses);

        builder.OwnsMany<UserAddress>("_addresses", address =>
        {
            address.ToTable("UserAddresses");
            address.WithOwner().HasForeignKey("UserId");
            address.HasKey(userAddress => userAddress.Id);
            address.Property(userAddress => userAddress.Id).ValueGeneratedOnAdd();
            address.Property(userAddress => userAddress.IsDefault).IsRequired();
            address.Property<bool>("IsDeleted").HasDefaultValue(false).IsRequired();
            address.OwnsOne(userAddress => userAddress.Details, details =>
            {
                details.Property(d => d.Street).HasColumnName("Street").HasMaxLength(300).IsRequired();
                details.Property(d => d.City).HasColumnName("City").HasMaxLength(120).IsRequired();
                details.Property(d => d.BuildingNumber).HasColumnName("BuildingNumber").HasMaxLength(50).IsRequired();
                details.Property(d => d.Floor).HasColumnName("Floor").HasMaxLength(50);
            });
            address.HasIndex("UserId")
                .IsUnique()
                .HasFilter("[IsDefault] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)")
                .HasDatabaseName("UX_UserAddresses_UserId_Default");
        });

        builder.Navigation("_addresses").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
