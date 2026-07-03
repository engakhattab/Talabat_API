using Talabat.Domain.Common;

namespace Talabat.Domain.ValueObjects;

public sealed record DeliveryAddressSnapshot
{
    public string Street { get; }

    public string City { get; }

    public string BuildingNumber { get; }

    public string? Floor { get; }

    public DeliveryAddressSnapshot(
        string street,
        string city,
        string buildingNumber,
        string? floor = null)
    {
        Street = Guard.RequiredText(street, nameof(street));
        City = Guard.RequiredText(city, nameof(city));
        BuildingNumber = Guard.RequiredText(buildingNumber, nameof(buildingNumber));
        Floor = Guard.OptionalText(floor);
    }
}
