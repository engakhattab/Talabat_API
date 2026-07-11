using Talabat.Application.Customers.Models;
using Talabat.Domain.Aggregates.Customer;

namespace Talabat.Application.Customers.Mapping;

public static class CustomerMapper
{
    public static CustomerProfile ToProfile(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var addresses = customer.Addresses
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Id)
            .Select(address => new CustomerAddressDetails(
                address.Id,
                address.Details.Street,
                address.Details.City,
                address.Details.BuildingNumber,
                address.Details.Floor,
                address.IsDefault))
            .ToList()
            .AsReadOnly();

        return new CustomerProfile(
            customer.Id,
            customer.FullName,
            customer.Age,
            customer.PhoneNumber,
            addresses);
    }
}
