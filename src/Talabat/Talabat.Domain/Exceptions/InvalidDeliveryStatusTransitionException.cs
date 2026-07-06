namespace Talabat.Domain.Exceptions;

public sealed class InvalidDeliveryStatusTransitionException : DomainException
{
    public InvalidDeliveryStatusTransitionException()
        : base("Delivery status transition is not allowed.")
    {
    }

    public InvalidDeliveryStatusTransitionException(string message)
        : base(message)
    {
    }
}
