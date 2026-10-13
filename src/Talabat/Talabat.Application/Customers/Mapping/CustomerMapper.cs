using Talabat.Application.Customers.Models;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Customers.Mapping;

public static class CustomerMapper
{
    public static CustomerProfile ToProfile(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var addresses = user.Addresses
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
            user.Id,
            user.FullName,
            user.Age ?? throw new InvalidOperationException("Customer profile age is required."),
            user.PhoneNumber,
            addresses);
    }
}
