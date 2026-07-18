using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.Abstractions;

public interface IUserCapabilityService
{
    Task<UseCaseResult<int>> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        int age,
        string? phoneNumber,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> RegisterDeliveryAgentApplicantAsync(
        string email,
        string password,
        string fullName,
        VehicleType vehicleType,
        string? phoneNumber,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> GrantCustomerCapabilityAsync(
        int userId,
        string fullName,
        int age,
        string? phoneNumber,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> ApproveDeliveryAgentAsync(
        int userId,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> RejectDeliveryAgentAsync(
        int userId,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> DeactivateUserAsync(
        int userId,
        CancellationToken ct = default);
}
