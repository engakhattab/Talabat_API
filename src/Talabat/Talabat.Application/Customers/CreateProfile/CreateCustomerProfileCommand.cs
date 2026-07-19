namespace Talabat.Application.Customers.CreateProfile;

public sealed record CreateCustomerProfileCommand(
    int UserId,
    string FullName,
    int Age,
    string? PhoneNumber);
