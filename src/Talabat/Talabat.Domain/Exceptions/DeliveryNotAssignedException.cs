namespace Talabat.Domain.Exceptions;

public sealed class DeliveryNotAssignedException : DomainException
{
    public DeliveryNotAssignedException()
        : base("Delivery is not assigned to an agent.")
    {
    }
}
