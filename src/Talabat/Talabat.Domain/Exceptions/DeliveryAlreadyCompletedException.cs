namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAlreadyCompletedException : DomainException
{
    public DeliveryAlreadyCompletedException()
        : base("Delivery is already completed and cannot be changed.")
    {
    }
}
