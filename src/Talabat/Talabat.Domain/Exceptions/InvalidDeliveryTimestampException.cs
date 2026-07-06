namespace Talabat.Domain.Exceptions;

public sealed class InvalidDeliveryTimestampException : DomainException
{
    public InvalidDeliveryTimestampException()
        : base("Delivery transition time cannot be earlier than the previous transition.")
    {
    }
}
