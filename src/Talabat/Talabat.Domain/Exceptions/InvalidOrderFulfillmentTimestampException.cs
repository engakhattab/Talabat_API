namespace Talabat.Domain.Exceptions;

public sealed class InvalidOrderFulfillmentTimestampException : DomainException
{
    public InvalidOrderFulfillmentTimestampException()
        : base("Order fulfillment transition time cannot be earlier than the previous transition.")
    {
    }
}
