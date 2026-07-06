namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAlreadyCompletedException : DomainException
{
    public DeliveryAlreadyCompletedException()
        : base("Delivery is in a terminal state and cannot be changed.")
    {
    }
}
