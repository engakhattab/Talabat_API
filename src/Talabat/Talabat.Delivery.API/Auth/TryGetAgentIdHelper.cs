using Talabat.Application.Abstractions;

namespace Talabat.Delivery.API.Auth;

public static class TryGetAgentIdHelper
{
    public static bool TryGetAgentId(ICurrentUser currentUser, out int agentId)
    {
        agentId = 0;
        if (!currentUser.HasDeliveryAgentCapability || currentUser.AgentId is null)
            return false;
        agentId = currentUser.AgentId.Value;
        return true;
    }
}
