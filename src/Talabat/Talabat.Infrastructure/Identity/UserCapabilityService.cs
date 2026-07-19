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
                roleResult = await SynchronizeCapabilityRolesAsync(user);
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
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
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

            var roleResult = await SynchronizeCapabilityRolesAsync(user);
            if (!roleResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(roleResult, ApplicationErrorCategory.Conflict));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
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
                roleResult = await SynchronizeCapabilityRolesAsync(user);
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
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
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
                roleResult = await SynchronizeCapabilityRolesAsync(user);
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
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
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

            var roleResult = await SynchronizeCapabilityRolesAsync(user);
            if (!roleResult.Succeeded)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    MapIdentityErrors(roleResult, ApplicationErrorCategory.Conflict));
            }

            await CommitOrReleaseAsync(transaction);
            return UseCaseResult<int>.Success(user.Id);
        }
        catch (DomainException ex)
        {
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
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

            var roleResult = await SynchronizeCapabilityRolesAsync(user);
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
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
    }

    public async Task<UseCaseResult<int>> SoftDeleteUserAsync(
        int userId,
        string? deletedBy,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = await EnsureTransactionAsync(ct);
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                await RollbackAsync(transaction);
                return UseCaseResult<int>.Failure(
                    new ApplicationError(ApplicationErrorCodes.UserNotFound, ApplicationErrorCategory.NotFound, "User not found."));
            }

            user.SoftDelete(DateTime.UtcNow, deletedBy);
            await _dbContext.SaveChangesAsync(ct);

            var roleResult = await SynchronizeCapabilityRolesAsync(user);
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
            return await FailAsync(ex);
        }
        catch (ArgumentException ex)
        {
            return await FailAsync(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
        catch
        {
            await RollbackAfterExceptionAsync();
            throw;
        }
    }

    private async Task<IDbContextTransaction> EnsureTransactionAsync(CancellationToken ct)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            var savepointName = $"sp_uc_{Guid.NewGuid():N}";
            await _dbContext.Database.CurrentTransaction.CreateSavepointAsync(savepointName, ct);
            _transactionContext = new TransactionContext(_dbContext.Database.CurrentTransaction, false, savepointName);
            return _dbContext.Database.CurrentTransaction;
        }

        var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        _transactionContext = new TransactionContext(transaction, true, null);
        return transaction;
    }

    private async Task CommitOrReleaseAsync(IDbContextTransaction transaction)
    {
        var context = _transactionContext ?? throw new InvalidOperationException("Transaction context was not initialized.");

        if (context.OwnsTransaction)
        {
            await transaction.CommitAsync();
        }
        else if (context.SavepointName is not null)
        {
            await transaction.ReleaseSavepointAsync(context.SavepointName);
        }

        _transactionContext = null;
    }

    private async Task RollbackAsync(IDbContextTransaction? transaction, CancellationToken ct = default)
    {
        var context = _transactionContext;

        if (transaction is not null)
        {
            if (context?.OwnsTransaction == true)
            {
                await transaction.RollbackAsync(ct);
            }
            else if (context?.SavepointName is not null)
            {
                await transaction.RollbackToSavepointAsync(context.SavepointName, ct);
            }
        }

        _dbContext.ChangeTracker.Clear();
        _transactionContext = null;
    }

    private static ApplicationError MapIdentityErrors(IdentityResult result, ApplicationErrorCategory category)
    {
        var description = string.Join("; ", result.Errors.Select(e => e.Description));
        return new ApplicationError(ApplicationErrorCodes.IdentityOperationFailed, category, description);
    }

    private async Task<IdentityResult> SynchronizeCapabilityRolesAsync(User user)
    {
        var desiredRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (user.UserType.HasFlag(UserType.Customer))
        {
            desiredRoles.Add(IdentityRoleNames.Customer);
        }
        if (user.UserType.HasFlag(UserType.DeliveryAgent))
        {
            desiredRoles.Add(IdentityRoleNames.DeliveryAgent);
        }
        if (user.UserType.HasFlag(UserType.Admin))
        {
            desiredRoles.Add(IdentityRoleNames.Admin);
        }
        if (user.UserType.HasFlag(UserType.RestaurantOwner))
        {
            desiredRoles.Add(IdentityRoleNames.RestaurantOwner);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToAdd = desiredRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        var rolesToRemove = currentRoles.Except(desiredRoles, StringComparer.OrdinalIgnoreCase).ToArray();

        if (rolesToAdd.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                return addResult;
            }
        }

        return rolesToRemove.Length == 0
            ? IdentityResult.Success
            : await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
    }

    private async Task<UseCaseResult<int>> FailAsync(Exception exception)
    {
        await RollbackAsync(_transactionContext?.Transaction);
        return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(exception));
    }

    private Task RollbackAfterExceptionAsync()
    {
        return RollbackAsync(_transactionContext?.Transaction, CancellationToken.None);
    }

    private TransactionContext? _transactionContext;

    private sealed record TransactionContext(
        IDbContextTransaction Transaction,
        bool OwnsTransaction,
        string? SavepointName);
}
