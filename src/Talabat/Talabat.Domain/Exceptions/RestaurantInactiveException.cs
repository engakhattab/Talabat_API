namespace Talabat.Domain.Exceptions;

public sealed class RestaurantInactiveException : DomainException
{
    public RestaurantInactiveException()
        : base("This restaurant is currently inactive.")
    {
    }
}
