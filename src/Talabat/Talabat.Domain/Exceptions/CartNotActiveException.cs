namespace Talabat.Domain.Exceptions;

public sealed class CartNotActiveException : DomainException
{
    public CartNotActiveException()
        : base("Only active carts can be modified.")
    {
    }
}
