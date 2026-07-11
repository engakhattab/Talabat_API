using Talabat.Domain.Common;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Customer;

public sealed class CustomerAddress
{
    public int Id { get; private set; }

    public Address Details { get; private set; }

    public bool IsDefault { get; private set; }

    private CustomerAddress()
    {
        Details = new Address("Materialization", "Materialization", "0");
    }

    internal CustomerAddress(Address details, bool isDefault)
    {
        Details = details ?? throw new ArgumentNullException(nameof(details));
        IsDefault = isDefault;
    }

    internal void MarkAsDefault()
    {
        IsDefault = true;
    }

    internal void MarkAsNonDefault()
    {
        IsDefault = false;
    }
}
