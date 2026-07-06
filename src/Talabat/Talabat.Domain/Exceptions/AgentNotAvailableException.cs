namespace Talabat.Domain.Exceptions;

public sealed class AgentNotAvailableException : DomainException
{
    public AgentNotAvailableException()
        : base("Delivery agent is not available.")
    {
    }
}
