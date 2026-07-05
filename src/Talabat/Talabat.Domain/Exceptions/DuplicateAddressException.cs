namespace Talabat.Domain.Exceptions;

public sealed class DuplicateAddressException : DomainException
{
    public DuplicateAddressException()
        : base("This address already exists for the customer.")
    {
    }
}
