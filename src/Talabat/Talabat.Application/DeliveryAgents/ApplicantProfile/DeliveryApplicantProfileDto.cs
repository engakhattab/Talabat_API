using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.DeliveryAgents.ApplicantProfile;

public sealed record DeliveryApplicantProfileDto(
    string FullName,
    string? PhoneNumber,
    VehicleType? VehicleType,
    AgentApprovalStatus ApplicationStatus,
    DeliveryAgentStatus? DeliveryAgentStatus);
