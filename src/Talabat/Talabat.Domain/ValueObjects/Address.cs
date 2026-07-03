using Talabat.Domain.Common;

namespace Talabat.Domain.ValueObjects;

public sealed class Address : IEquatable<Address>
{
    private static readonly StringComparer FieldComparer = StringComparer.OrdinalIgnoreCase;

    public string Street { get; }

    public string City { get; }

    public string BuildingNumber { get; }

    public string? Floor { get; }

    public Address(string street, string city, string buildingNumber, string? floor = null)
    {
        Street = Guard.RequiredText(street, nameof(street));
        City = Guard.RequiredText(city, nameof(city));
        BuildingNumber = Guard.RequiredText(buildingNumber, nameof(buildingNumber));
        Floor = Guard.OptionalText(floor);
    }

    public bool Equals(Address? other)
    {
        return other is not null
            && FieldComparer.Equals(Street, other.Street)
            && FieldComparer.Equals(City, other.City)
            && FieldComparer.Equals(BuildingNumber, other.BuildingNumber)
            && FieldComparer.Equals(Floor, other.Floor);
    }

    public override bool Equals(object? obj)
    {
        return obj is Address other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Street, FieldComparer);
        hash.Add(City, FieldComparer);
        hash.Add(BuildingNumber, FieldComparer);
        hash.Add(Floor, FieldComparer);
        return hash.ToHashCode();
    }

    public static bool operator ==(Address? left, Address? right)
    {
        return EqualityComparer<Address>.Default.Equals(left, right);
    }

    public static bool operator !=(Address? left, Address? right)
    {
        return !(left == right);
    }
}
