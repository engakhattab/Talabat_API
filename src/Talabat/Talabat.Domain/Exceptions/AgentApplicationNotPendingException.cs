namespace Talabat.Domain.Exceptions;

public sealed class AgentApplicationNotPendingException : DomainException
{
    public AgentApplicationNotPendingException()
        : base("The delivery agent application is not in a pending state.")
    {
    }
}
