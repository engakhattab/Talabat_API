namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAgentMismatchException : DomainException
{
    public DeliveryAgentMismatchException()
        : base("Delivery is assigned to a different agent.")
    {
    }
}
