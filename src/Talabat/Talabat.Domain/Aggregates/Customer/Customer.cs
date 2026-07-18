using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Customer;

public sealed class Customer : AuditableEntity
{
    private readonly List<CustomerAddress> _addresses = [];

    public int Id { get; private set; }

    public string FullName { get; private set; }

    public int Age { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? IdentityUserId { get; private set; }

    public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();

    private Customer()
    {
        FullName = string.Empty;
    }

    public Customer(string fullName, int age, string? phoneNumber = null)
    {
        FullName = Guard.RequiredText(fullName, nameof(fullName));
        Age = Guard.Positive(age, nameof(age));
        PhoneNumber = Guard.OptionalText(phoneNumber);
    }

    public static Customer CreateForAccount(
        string identityUserId,
        string fullName,
        int age,
        string? phoneNumber = null)
    {
        var normalizedIdentityUserId = Guard.RequiredText(identityUserId, nameof(identityUserId));

        return new Customer(fullName, age, phoneNumber)
        {
            IdentityUserId = normalizedIdentityUserId
        };
    }

    public void UpdateProfile(string fullName, int age, string? phoneNumber = null)
    {
        SetProfile(fullName, age, phoneNumber);
    }

    public void AddAddress(Address address, bool makeDefault = false)
    {
        ArgumentNullException.ThrowIfNull(address);

        var customerAddress = new CustomerAddress(address, makeDefault);

        if (_addresses.Any(existingAddress => existingAddress.Details == address))
        {
            throw new DuplicateAddressException();
        }

        if (makeDefault)
        {
            MarkAllAddressesAsNonDefault();
        }

        _addresses.Add(customerAddress);
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

    public DeliveryAddressSnapshot CreateDeliveryAddressSnapshot(int addressId)
    {
        var address = GetRequiredAddress(addressId).Details;

        return new DeliveryAddressSnapshot(
            address.Street,
            address.City,
            address.BuildingNumber,
            address.Floor);
    }

    private CustomerAddress GetRequiredAddress(int addressId)
    {
        Guard.Positive(addressId, nameof(addressId));

        return _addresses.SingleOrDefault(address => address.Id == addressId)
            ?? throw new AddressNotFoundException();
    }

    private void SetProfile(string fullName, int age, string? phoneNumber)
    {
        var normalizedFullName = Guard.RequiredText(fullName, nameof(fullName));
        var validAge = Guard.Positive(age, nameof(age));
        var normalizedPhoneNumber = Guard.OptionalText(phoneNumber);

        FullName = normalizedFullName;
        Age = validAge;
        PhoneNumber = normalizedPhoneNumber;
    }

    private void MarkAllAddressesAsNonDefault()
    {
        foreach (var address in _addresses)
        {
            address.MarkAsNonDefault();
        }
    }
}
