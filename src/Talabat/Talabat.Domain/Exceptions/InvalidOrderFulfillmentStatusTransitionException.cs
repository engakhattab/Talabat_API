namespace Talabat.Domain.Exceptions;

public sealed class InvalidOrderFulfillmentStatusTransitionException : DomainException
{
    public InvalidOrderFulfillmentStatusTransitionException()
        : base("Order fulfillment cannot make this transition from its current status.")
    {
    }
}
