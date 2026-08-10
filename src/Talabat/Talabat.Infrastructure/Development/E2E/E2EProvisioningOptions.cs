namespace Talabat.Infrastructure.Development.E2E;

public sealed class E2EProvisioningOptions
{
    public const string SectionName = "E2E:Provisioning";

    public bool Enabled { get; set; }

    public string Marker { get; set; } = string.Empty;

    public string CustomerEmailMarker { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Age { get; set; }

    public string? PhoneNumber { get; set; }

    public E2EAddressOptions DefaultAddress { get; set; } = new();

    public E2EAddressOptions AlternateAddress { get; set; } = new();

    public E2ECatalogOptions Catalog { get; set; } = new();
}

public sealed class E2EAddressOptions
{
    public string Label { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string BuildingNumber { get; set; } = string.Empty;

    public string? Floor { get; set; }
}

public sealed class E2ECatalogOptions
{
    public string RestaurantName { get; set; } = string.Empty;

    public string HappyProductName { get; set; } = string.Empty;

    public decimal HappyProductPrice { get; set; }

    public string NegativeProductName { get; set; } = string.Empty;

    public decimal NegativeProductPrice { get; set; }
}
