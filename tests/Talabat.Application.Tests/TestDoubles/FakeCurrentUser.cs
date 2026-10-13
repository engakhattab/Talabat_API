using Talabat.Application.Abstractions;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;

    public int? UserId { get; set; }

    public bool HasCustomerCapability { get; set; }

    public int? CustomerId { get; set; }

    public bool HasDeliveryAgentCapability { get; set; }

    public int? AgentId { get; set; }
}
