using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Identity;

public sealed class UserCapabilityService : IUserCapabilityService
{
    private readonly TalabatDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private bool _ownsTransaction;
    private string? _savepointName;

    public UserCapabilityService(TalabatDbContext dbContext, UserManager<User> userManager)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<UseCaseResult<int>> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        int age,
        string? phoneNumber,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);

            var user = User.Register(email, email, fullName);
            user.InitializeCustomerProfile(fullName, age, phoneNumber);

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(createResult, ApplicationErrorCategory.Validation));
            }

            IdentityResult roleResult;
            try
            {
                roleResult = await _userManager.AddToRoleAsync(user, IdentityRoleNames.Customer);
            }
            catch (InvalidOperationException ex)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.IdentityOperationFailed, ApplicationErrorCategory.Conflict, ex.Message));
            }

            if (!roleResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(roleResult, ApplicationErrorCategory.Conflict));
            }

            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(stampResult, ApplicationErrorCategory.Conflict));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<UseCaseResult<int>> RegisterDeliveryAgentApplicantAsync(
        string email,
        string password,
        string fullName,
        VehicleType vehicleType,
        string? phoneNumber,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);

            var user = User.Register(email, email, fullName);
            user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
            user.SubmitDeliveryAgentApplication(vehicleType);

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(createResult, ApplicationErrorCategory.Validation));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<UseCaseResult<int>> GrantCustomerCapabilityAsync(
        int userId,
        string fullName,
        int age,
        string? phoneNumber,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.UserNotFound, ApplicationErrorCategory.NotFound, "User not found."));
            }

            if (user.UserType.HasFlag(UserType.Customer))
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.ProfileAlreadyExists, ApplicationErrorCategory.Conflict, "Customer profile already exists."));
            }

            user.InitializeCustomerProfile(fullName, age, phoneNumber);
            await _dbContext.SaveChangesAsync(ct);

            IdentityResult roleResult;
            try
            {
                roleResult = await _userManager.AddToRoleAsync(user, IdentityRoleNames.Customer);
            }
            catch (InvalidOperationException ex)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.IdentityOperationFailed, ApplicationErrorCategory.Conflict, ex.Message));
            }

            if (!roleResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(roleResult, ApplicationErrorCategory.Conflict));
            }

            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(stampResult, ApplicationErrorCategory.Conflict));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<UseCaseResult<int>> ApproveDeliveryAgentAsync(
        int userId,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.UserNotFound, ApplicationErrorCategory.NotFound, "User not found."));
            }

            user.ApproveDeliveryAgentApplication();
            await _dbContext.SaveChangesAsync(ct);

            IdentityResult roleResult;
            try
            {
                roleResult = await _userManager.AddToRoleAsync(user, IdentityRoleNames.DeliveryAgent);
            }
            catch (InvalidOperationException ex)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.IdentityOperationFailed, ApplicationErrorCategory.Conflict, ex.Message));
            }

            if (!roleResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(roleResult, ApplicationErrorCategory.Conflict));
            }

            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(stampResult, ApplicationErrorCategory.Conflict));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<UseCaseResult<int>> RejectDeliveryAgentAsync(
        int userId,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.UserNotFound, ApplicationErrorCategory.NotFound, "User not found."));
            }

            user.RejectDeliveryAgentApplication();
            await _dbContext.SaveChangesAsync(ct);

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<UseCaseResult<int>> DeactivateUserAsync(
        int userId,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.UserNotFound, ApplicationErrorCategory.NotFound, "User not found."));
            }

            user.Deactivate();
            await _dbContext.SaveChangesAsync(ct);

            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(stampResult, ApplicationErrorCategory.Conflict));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private async Task<IDbContextTransaction> EnsureTransactionAsync(CancellationToken ct)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            _savepointName = $"sp_uc_{Guid.NewGuid():N}";
            await _dbContext.Database.CurrentTransaction.CreateSavepointAsync(_savepointName, ct);
            return _dbContext.Database.CurrentTransaction;
        }

        _ownsTransaction = true;
        return await _dbContext.Database.BeginTransactionAsync(ct);
    }

    private async Task CommitOrReleaseAsync(IDbContextTransaction transaction)
    {
        if (_ownsTransaction)
        {
            await transaction.CommitAsync();
        }
        else if (_savepointName is not null)
        {
            await transaction.ReleaseSavepointAsync(_savepointName);
        }
    }

    private async Task RollbackAsync(IDbContextTransaction? transaction, CancellationToken ct = default)
    {
        if (transaction is not null)
        {
            if (_ownsTransaction)
            {
                await transaction.RollbackAsync(ct);
            }
            else if (_savepointName is not null)
            {
                await transaction.RollbackToSavepointAsync(_savepointName, ct);
            }
        }

        _dbContext.ChangeTracker.Clear();
    }

    private static ApplicationError MapIdentityErrors(IdentityResult result, ApplicationErrorCategory category)
    {
        var description = string.Join("; ", result.Errors.Select(e => e.Description));
        return new ApplicationError(ApplicationErrorCodes.IdentityOperationFailed, category, description);
    }
}
