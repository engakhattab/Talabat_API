namespace Talabat.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    bool HasCustomerCapability { get; }

    int? CustomerId { get; }

    bool HasDeliveryAgentCapability { get; }

    int? AgentId { get; }
}
