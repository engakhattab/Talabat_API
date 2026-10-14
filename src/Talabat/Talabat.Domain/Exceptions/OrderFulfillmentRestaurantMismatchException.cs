namespace Talabat.Domain.Exceptions;

public sealed class OrderFulfillmentRestaurantMismatchException : DomainException
{
    public OrderFulfillmentRestaurantMismatchException()
        : base("Order fulfillment belongs to a different restaurant.")
    {
    }
}
