namespace Talabat.Domain.Exceptions;

public sealed class DeliveryAgentApplicationNotFoundException : DomainException
{
    public DeliveryAgentApplicationNotFoundException()
        : base("The delivery agent application was not found.")
    {
    }
}
