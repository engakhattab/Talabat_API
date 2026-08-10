using Talabat.Domain.Aggregates.Users;

namespace Talabat.Delivery.API.Contracts.ApplicantProfile;

public sealed record DeliveryApplicantProfileResponse(
    string FullName,
    string? PhoneNumber,
    VehicleType? VehicleType,
    AgentApprovalStatus ApplicationStatus,
    DeliveryAgentStatus? DeliveryAgentStatus);
