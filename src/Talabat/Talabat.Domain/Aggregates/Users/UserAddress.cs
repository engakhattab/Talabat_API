using Talabat.Domain.Common;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Users;

public sealed class UserAddress
{
    public int Id { get; private set; }

    public Address Details { get; private set; }

    public bool IsDefault { get; private set; }

    private UserAddress()
    {
        Details = new Address("Materialization", "Materialization", "0");
    }

    internal UserAddress(Address details, bool isDefault)
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
