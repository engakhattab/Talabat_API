namespace Talabat.Domain.Exceptions;

public sealed class InvalidDeliveryAgentStatusTransitionException : DomainException
{
    public InvalidDeliveryAgentStatusTransitionException(string message)
        : base(message)
    {
    }
}
