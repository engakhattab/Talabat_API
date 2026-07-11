namespace Talabat.Application.Customers.RemoveAddress;

public sealed record RemoveCustomerAddressCommand(int CustomerId, int AddressId);
