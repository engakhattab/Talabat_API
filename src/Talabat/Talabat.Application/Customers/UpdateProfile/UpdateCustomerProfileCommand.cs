namespace Talabat.Application.Customers.UpdateProfile;

public sealed record UpdateCustomerProfileCommand(
    int CustomerId,
    string FullName,
    int Age,
    string? PhoneNumber);
