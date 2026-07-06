namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAlreadyAssignedException : DomainException
{
    public DeliveryAlreadyAssignedException()
        : base("Delivery is already assigned to an agent.")
    {
    }
}
