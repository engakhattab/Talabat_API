namespace Talabat.Domain.Exceptions;

public sealed class CartRestaurantMismatchException : DomainException
{
    public CartRestaurantMismatchException()
        : base("The cart does not belong to the specified restaurant.")
    {
    }
}
