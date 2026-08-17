using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.DeliveryAgents.ApplicationReview;

public sealed record DeliveryAgentApplicationReviewDto(
    int UserId,
    string FullName,
    string? Email,
    string? PhoneNumber,
    VehicleType? VehicleType,
    AgentApprovalStatus ApplicationStatus,
    DateTime SubmittedAt);
