namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAgentNotInitializedException : DomainException
{
    public DeliveryAgentNotInitializedException()
        : base("Delivery agent has not been initialized for this user.")
    {
    }
}
