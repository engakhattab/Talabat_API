namespace Talabat.Application.Customers.Models;

public sealed record CustomerProfile(
    int Id,
    string FullName,
    int Age,
    string? PhoneNumber,
    IReadOnlyCollection<CustomerAddressDetails> Addresses);
