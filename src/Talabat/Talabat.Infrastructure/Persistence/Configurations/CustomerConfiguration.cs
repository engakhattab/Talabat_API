using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talabat.Domain.Aggregates.Customer;

namespace Talabat.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable(
            "Customers",
            table => table.HasCheckConstraint("CK_Customers_Age_Positive", "[Age] > 0"));

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();
        builder.Ignore(customer => customer.Addresses);

        builder.Property(customer => customer.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(customer => customer.Age)
            .IsRequired();

        builder.Property(customer => customer.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(customer => customer.IdentityUserId)
            .HasMaxLength(450);

        builder.HasIndex(customer => customer.IdentityUserId)
            .IsUnique()
            .HasFilter("[IdentityUserId] IS NOT NULL")
            .HasDatabaseName("UX_Customers_IdentityUserId");

        builder.OwnsMany<CustomerAddress>(
            "_addresses",
            address =>
            {
                address.ToTable("CustomerAddresses");
                address.WithOwner().HasForeignKey("CustomerId");
                address.HasKey(customerAddress => customerAddress.Id);

                address.Property(customerAddress => customerAddress.Id)
                    .ValueGeneratedOnAdd();

                address.Property(customerAddress => customerAddress.IsDefault)
                    .IsRequired();

                address.Property<bool>("IsDeleted")
                    .HasDefaultValue(false)
                    .IsRequired();

                address.OwnsOne(
                    customerAddress => customerAddress.Details,
                    details => details.ConfigureAddress());

                address.HasIndex("CustomerId")
                    .IsUnique()
                    .HasFilter("[IsDefault] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)")
                    .HasDatabaseName("UX_CustomerAddresses_CustomerId_Default");
            });

        builder.Navigation("_addresses")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
