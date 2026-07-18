namespace Talabat.Domain.Aggregates.Users;

[Flags]
public enum UserType
{
    None = 0,
    Customer = 1,
    DeliveryAgent = 2,
    Admin = 4,
    RestaurantOwner = 8
}
