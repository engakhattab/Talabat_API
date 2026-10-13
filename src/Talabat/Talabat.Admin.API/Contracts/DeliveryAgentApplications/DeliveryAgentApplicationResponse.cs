using Talabat.Domain.Aggregates.Users;

namespace Talabat.Admin.API.Contracts.DeliveryAgentApplications;

public sealed record DeliveryAgentApplicationResponse(
    int UserId,
    string FullName,
    string? Email,
    string? PhoneNumber,
    VehicleType? VehicleType,
    AgentApprovalStatus ApplicationStatus,
    DateTime SubmittedAt);
