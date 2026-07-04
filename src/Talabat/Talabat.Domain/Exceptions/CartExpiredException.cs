namespace Talabat.Domain.Exceptions;

public sealed class CartExpiredException : DomainException
{
    public CartExpiredException()
        : base("This cart has expired. Please start a new cart.")
    {
    }
}
