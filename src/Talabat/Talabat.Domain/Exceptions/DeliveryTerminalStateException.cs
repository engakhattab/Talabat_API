namespace Talabat.Domain.Exceptions;

public sealed class DeliveryTerminalStateException : DomainException
{
    public DeliveryTerminalStateException()
        : base("Delivery is in a terminal state and cannot be changed.")
    {
    }
}
