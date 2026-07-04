namespace Talabat.Domain.Exceptions;

public sealed class EmptyCartCheckoutException : DomainException
{
    public EmptyCartCheckoutException()
        : base("Cannot checkout an empty cart.")
    {
    }
}
