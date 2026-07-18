namespace Talabat.Application.Customers.CreateProfile;

public sealed record CreateCustomerProfileCommand(
    string IdentityUserId,
    string FullName,
    int Age,
    string? PhoneNumber);
