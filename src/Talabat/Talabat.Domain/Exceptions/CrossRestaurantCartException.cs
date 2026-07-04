namespace Talabat.Domain.Exceptions;

public sealed class CrossRestaurantCartException : DomainException
{
    public CrossRestaurantCartException()
        : base("Cannot add items from a different restaurant. Clear the cart first.")
    {
    }
}
