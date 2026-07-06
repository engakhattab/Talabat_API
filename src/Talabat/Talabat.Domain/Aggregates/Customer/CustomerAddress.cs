using Talabat.Domain.Common;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Customer;

public sealed class CustomerAddress
{
    public int Id { get; }

    public Address Details { get; }

    public bool IsDefault { get; private set; }

    internal CustomerAddress(int id, Address details, bool isDefault)
    {
        Id = Guard.Positive(id, nameof(id));
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
