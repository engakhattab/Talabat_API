using Talabat.Domain.Aggregates.Users;

namespace Talabat.Delivery.API.Contracts.ApplicantProfile;

public sealed record DeliveryApplicantApplicationResponse(AgentApprovalStatus ApplicationStatus);
