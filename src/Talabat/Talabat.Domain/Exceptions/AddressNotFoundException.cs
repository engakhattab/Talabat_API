namespace Talabat.Domain.Exceptions;

public sealed class AddressNotFoundException : DomainException
{
    public AddressNotFoundException()
        : base("Address was not found for this customer.")
    {
    }
}
