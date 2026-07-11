namespace Talabat.Application.Customers.SetDefaultAddress;

public sealed record SetDefaultCustomerAddressCommand(int CustomerId, int AddressId);
