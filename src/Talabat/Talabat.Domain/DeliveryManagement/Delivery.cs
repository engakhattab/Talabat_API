using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.DeliveryManagement;

public sealed class Delivery
{
    public int Id { get; }

    public int OrderId { get; }

    public int CustomerId { get; }

    public int RestaurantId { get; }

    public int? AssignedAgentId { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public DeliveryAddressSnapshot DeliveryAddress { get; }

    public DateTime CreatedAt { get; }

    public DateTime? AssignedAt { get; private set; }

    public DateTime? ArrivedAtRestaurantAt { get; private set; }

    public DateTime? PickedUpAt { get; private set; }

    public DateTime? OutForDeliveryAt { get; private set; }

    public DateTime? DeliveredAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public DateTime? FailedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public Delivery(
        int id,
        int orderId,
        int customerId,
        int restaurantId,
        DeliveryAddressSnapshot deliveryAddress,
        DateTime createdAt)
    {
        Id = Guard.Positive(id, nameof(id));
        OrderId = Guard.Positive(orderId, nameof(orderId));
        CustomerId = Guard.Positive(customerId, nameof(customerId));
        RestaurantId = Guard.Positive(restaurantId, nameof(restaurantId));
        DeliveryAddress = deliveryAddress
            ?? throw new ArgumentNullException(nameof(deliveryAddress));
        CreatedAt = createdAt;
        Status = DeliveryStatus.PendingAssignment;
    }

    public void AssignAgent(int agentId, DateTime currentTime)
    {
        agentId = Guard.Positive(agentId, nameof(agentId));
        EnsureNotTerminal();

        if (AssignedAgentId.HasValue)
        {
            throw new DeliveryAlreadyAssignedException();
        }

        EnsureStatus(DeliveryStatus.PendingAssignment);

        AssignedAgentId = agentId;
        AssignedAt = currentTime;
        Status = DeliveryStatus.Assigned;
    }

    public void MarkArrivedAtRestaurant(int agentId, DateTime currentTime)
    {
        EnsureNotTerminal();
        EnsureAssignedToAgent(agentId);
        EnsureStatus(DeliveryStatus.Assigned);

        Status = DeliveryStatus.ArrivedAtRestaurant;
        ArrivedAtRestaurantAt = currentTime;
    }

    public void MarkPickedUp(int agentId, DateTime currentTime)
    {
        EnsureNotTerminal();
        EnsureAssignedToAgent(agentId);
        EnsureStatus(DeliveryStatus.ArrivedAtRestaurant);

        Status = DeliveryStatus.PickedUp;
        PickedUpAt = currentTime;
    }

    public void MarkOutForDelivery(int agentId, DateTime currentTime)
    {
        EnsureNotTerminal();
        EnsureAssignedToAgent(agentId);
        EnsureStatus(DeliveryStatus.PickedUp);

        Status = DeliveryStatus.OutForDelivery;
        OutForDeliveryAt = currentTime;
    }

    public void MarkDelivered(int agentId, DateTime currentTime)
    {
        EnsureNotTerminal();
        EnsureAssignedToAgent(agentId);
        EnsureStatus(DeliveryStatus.OutForDelivery);

        Status = DeliveryStatus.Delivered;
        DeliveredAt = currentTime;
    }

    public void Cancel(DateTime currentTime)
    {
        EnsureNotTerminal();

        if (Status is DeliveryStatus.PickedUp or DeliveryStatus.OutForDelivery)
        {
            throw new InvalidDeliveryStatusTransitionException();
        }

        Status = DeliveryStatus.Cancelled;
        CancelledAt = currentTime;
    }

    public void Fail(string reason, DateTime currentTime)
    {
        EnsureNotTerminal();

        FailureReason = Guard.RequiredText(reason, nameof(reason));
        Status = DeliveryStatus.Failed;
        FailedAt = currentTime;
    }

    public bool IsTerminal()
    {
        return Status is DeliveryStatus.Delivered
            or DeliveryStatus.Cancelled
            or DeliveryStatus.Failed;
    }

    public bool IsActive()
    {
        return Status is DeliveryStatus.Assigned
            or DeliveryStatus.ArrivedAtRestaurant
            or DeliveryStatus.PickedUp
            or DeliveryStatus.OutForDelivery;
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal())
        {
            throw new DeliveryAlreadyCompletedException();
        }
    }

    private void EnsureAssignedToAgent(int agentId)
    {
        agentId = Guard.Positive(agentId, nameof(agentId));

        if (!AssignedAgentId.HasValue)
        {
            throw new DeliveryNotAssignedException();
        }

        if (AssignedAgentId.Value != agentId)
        {
            throw new DeliveryAgentMismatchException();
        }
    }

    private void EnsureStatus(DeliveryStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidDeliveryStatusTransitionException();
        }
    }
}
