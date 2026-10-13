namespace Talabat.Application.DeliveryAgents.UpdateLocation;

public sealed record UpdateLocationCommand(int AgentId, decimal Latitude, decimal Longitude);
