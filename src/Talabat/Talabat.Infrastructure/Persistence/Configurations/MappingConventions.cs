using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal static class MappingConventions
{
    public static void ConfigureIdentityKey<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.HasKey("Id");
        builder.Property<int>("Id").ValueGeneratedOnAdd();
    }

    public static void ConfigureAuditing<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IAuditable, ISoftDeletable
    {
        builder.Property(entity => entity.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(entity => entity.CreatedBy).HasMaxLength(200);
        builder.Property(entity => entity.ModifiedAt).HasColumnType("datetime2");
        builder.Property(entity => entity.ModifiedBy).HasMaxLength(200);
        builder.Property(entity => entity.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.DeletedAt).HasColumnType("datetime2");
        builder.Property(entity => entity.DeletedBy).HasMaxLength(200);
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }

    public static void ConfigureAuditableEntity<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.ConfigureAuditing<TEntity>();
    }

    public static void ConfigureMoney<TOwner>(
        this OwnedNavigationBuilder<TOwner, Money> builder,
        string amountColumnName)
        where TOwner : class
    {
        builder.Property(money => money.Amount)
            .HasColumnName(amountColumnName)
            .HasPrecision(18, 2)
            .IsRequired();
    }

    public static void ConfigureAddress<TOwner>(
        this OwnedNavigationBuilder<TOwner, Address> builder)
        where TOwner : class
    {
        builder.Property(address => address.Street)
            .HasColumnName("Street")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(address => address.City)
            .HasColumnName("City")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(address => address.BuildingNumber)
            .HasColumnName("BuildingNumber")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(address => address.Floor)
            .HasColumnName("Floor")
            .HasMaxLength(50);
    }

    public static void ConfigureDeliveryAddressSnapshot<TOwner>(
        this OwnedNavigationBuilder<TOwner, DeliveryAddressSnapshot> builder)
        where TOwner : class
    {
        builder.Property(address => address.Street)
            .HasColumnName("DeliveryStreet")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(address => address.City)
            .HasColumnName("DeliveryCity")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(address => address.BuildingNumber)
            .HasColumnName("DeliveryBuildingNumber")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(address => address.Floor)
            .HasColumnName("DeliveryFloor")
            .HasMaxLength(50);
    }

    public static void ConfigureTimeRange<TOwner>(
        this OwnedNavigationBuilder<TOwner, TimeRange> builder)
        where TOwner : class
    {
        builder.Property(timeRange => timeRange.Start)
            .HasColumnName("OpeningStart")
            .HasColumnType("time")
            .IsRequired();

        builder.Property(timeRange => timeRange.End)
            .HasColumnName("OpeningEnd")
            .HasColumnType("time")
            .IsRequired();
    }

    public static void ConfigureGeoLocation<TOwner>(
        this OwnedNavigationBuilder<TOwner, GeoLocation> builder)
        where TOwner : class
    {
        builder.Property(location => location.Latitude)
            .HasColumnName("CurrentLatitude")
            .HasPrecision(9, 6);

        builder.Property(location => location.Longitude)
            .HasColumnName("CurrentLongitude")
            .HasPrecision(9, 6);
    }
}
