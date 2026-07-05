using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Customer;

public sealed class Customer
{
    private readonly List<CustomerAddress> _addresses = [];

    public int Id { get; }

    public string FullName { get; private set; }

    public int Age { get; private set; }

    public string? PhoneNumber { get; private set; }

    public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();

    public Customer(int id, string fullName, int age, string? phoneNumber = null)
    {
        Id = Guard.Positive(id, nameof(id));
        FullName = Guard.RequiredText(fullName, nameof(fullName));
        Age = Guard.Positive(age, nameof(age));
        PhoneNumber = Guard.OptionalText(phoneNumber);
    }

    public void UpdateProfile(string fullName, int age, string? phoneNumber = null)
    {
        SetProfile(fullName, age, phoneNumber);
    }

    public void AddAddress(int addressId, Address address, bool makeDefault = false)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_addresses.Any(customerAddress => customerAddress.Details == address))
        {
            throw new DuplicateAddressException();
        }

        if (makeDefault)
        {
            MarkAllAddressesAsNonDefault();
        }

        _addresses.Add(new CustomerAddress(addressId, address, makeDefault));
    }

    public void RemoveAddress(int addressId)
    {
        _addresses.Remove(GetRequiredAddress(addressId));
    }

    public void SetDefaultAddress(int addressId)
    {
        var selectedAddress = GetRequiredAddress(addressId);

        MarkAllAddressesAsNonDefault();
        selectedAddress.MarkAsDefault();
    }

    private CustomerAddress GetRequiredAddress(int addressId)
    {
        Guard.Positive(addressId, nameof(addressId));

        return _addresses.SingleOrDefault(address => address.Id == addressId)
            ?? throw new AddressNotFoundException();
    }

    private void SetProfile(string fullName, int age, string? phoneNumber)
    {
        FullName = Guard.RequiredText(fullName, nameof(fullName));
        Age = Guard.Positive(age, nameof(age));
        PhoneNumber = Guard.OptionalText(phoneNumber);
    }

    private void MarkAllAddressesAsNonDefault()
    {
        foreach (var address in _addresses)
        {
            address.MarkAsNonDefault();
        }
    }
}
