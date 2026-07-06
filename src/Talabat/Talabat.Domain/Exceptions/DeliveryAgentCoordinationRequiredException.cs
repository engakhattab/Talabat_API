namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAgentCoordinationRequiredException : DomainException
{
    public DeliveryAgentCoordinationRequiredException()
        : base("Assigned delivery termination must coordinate the delivery agent.")
    {
    }
}
