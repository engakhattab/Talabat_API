using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.DeliveryManagement;

public sealed class DeliveryAgent
{
    public int Id { get; }

    public string FullName { get; }

    public string? PhoneNumber { get; }

    public VehicleType VehicleType { get; }

    public DeliveryAgentStatus Status { get; private set; }

    public GeoLocation? CurrentLocation { get; private set; }

    public DateTime CreatedAt { get; }

    public DeliveryAgent(
        int id,
        string fullName,
        VehicleType vehicleType,
        DateTime createdAt,
        string? phoneNumber = null,
        GeoLocation? currentLocation = null)
    {
        Id = Guard.Positive(id, nameof(id));
        FullName = Guard.RequiredText(fullName, nameof(fullName));
        PhoneNumber = Guard.OptionalText(phoneNumber);
        VehicleType = vehicleType;
        CreatedAt = createdAt;
        CurrentLocation = currentLocation;
        Status = DeliveryAgentStatus.Offline;
    }

    public bool IsAvailable()
    {
        return Status == DeliveryAgentStatus.Available;
    }

    public void GoOnline()
    {
        if (Status is DeliveryAgentStatus.Suspended or DeliveryAgentStatus.Busy)
        {
            throw new AgentNotAvailableException();
        }

        Status = DeliveryAgentStatus.Available;
    }

    public void GoOffline()
    {
        if (Status == DeliveryAgentStatus.Busy)
        {
            throw new InvalidOperationException(
                "A busy delivery agent cannot go offline.");
        }

        if (Status == DeliveryAgentStatus.Suspended)
        {
            throw new InvalidOperationException(
                "A suspended delivery agent cannot change online status.");
        }

        Status = DeliveryAgentStatus.Offline;
    }

    public void Suspend()
    {
        if (Status == DeliveryAgentStatus.Busy)
        {
            throw new InvalidOperationException(
                "A busy delivery agent cannot be suspended.");
        }

        Status = DeliveryAgentStatus.Suspended;
    }

    internal void MarkBusy()
    {
        if (!IsAvailable())
        {
            throw new AgentNotAvailableException();
        }

        Status = DeliveryAgentStatus.Busy;
    }

    internal void MarkAvailable()
    {
        if (Status == DeliveryAgentStatus.Suspended)
        {
            throw new AgentNotAvailableException();
        }

        Status = DeliveryAgentStatus.Available;
    }

    public void UpdateLocation(GeoLocation location)
    {
        CurrentLocation = location ?? throw new ArgumentNullException(nameof(location));
    }
}
