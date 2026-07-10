using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.DeliveryManagement;

public sealed class DeliveryAgent : AuditableEntity
{
    public int Id { get; private set; }

    public string FullName { get; private set; }

    public string? PhoneNumber { get; private set; }

    public VehicleType VehicleType { get; private set; }

    public DeliveryAgentStatus Status { get; private set; }

    public GeoLocation? CurrentLocation { get; private set; }

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

        if (!Enum.IsDefined(vehicleType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(vehicleType),
                vehicleType,
                "Vehicle type is not supported.");
        }

        VehicleType = vehicleType;
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
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
            throw new InvalidDeliveryAgentStatusTransitionException(
                "A busy delivery agent cannot go offline.");
        }

        if (Status == DeliveryAgentStatus.Suspended)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
                "A suspended delivery agent cannot change online status.");
        }

        Status = DeliveryAgentStatus.Offline;
    }

    public void Suspend()
    {
        if (Status == DeliveryAgentStatus.Busy)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
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
        if (Status != DeliveryAgentStatus.Busy)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
                "Only a busy delivery agent can be released as available.");
        }

        Status = DeliveryAgentStatus.Available;
    }

    public void UpdateLocation(GeoLocation location)
    {
        CurrentLocation = location ?? throw new ArgumentNullException(nameof(location));
    }
}
