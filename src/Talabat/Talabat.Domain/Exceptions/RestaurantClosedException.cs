namespace Talabat.Domain.Exceptions;

public sealed class RestaurantClosedException : DomainException
{
    public RestaurantClosedException()
        : base("This restaurant is currently closed.")
    {
    }
}
