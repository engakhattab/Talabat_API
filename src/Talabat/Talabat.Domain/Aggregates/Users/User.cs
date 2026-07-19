using Microsoft.AspNetCore.Identity;
using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;
using AgentApproval = Talabat.Domain.Aggregates.Users.AgentApprovalStatus;

namespace Talabat.Domain.Aggregates.Users;

public sealed class User : IdentityUser<int>, Common.Abstractions.IAuditable, Common.Abstractions.ISoftDeletable
{
    private readonly List<UserAddress> _addresses = [];

    public string FullName { get; private set; }

    public int? Age { get; private set; }

    public UserType UserType { get; private set; }

    public bool IsActive { get; private set; }

    public VehicleType? VehicleType { get; private set; }

    public DeliveryAgentStatus? DeliveryAgentStatus { get; private set; }

    public GeoLocation? CurrentLocation { get; private set; }

    public AgentApprovalStatus? AgentApprovalStatus { get; private set; }

    public byte[] RowVersion { get; private set; }

    // IAuditable
    public DateTime CreatedAt { get; protected set; }

    public string? CreatedBy { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<UserAddress> Addresses => _addresses.AsReadOnly();

    private User()
    {
        FullName = string.Empty;
        RowVersion = [];
    }

    public static User Register(string userName, string email, string fullName)
    {
        var normalizedFullName = Guard.RequiredText(fullName, nameof(fullName));

        return new User
        {
            UserName = userName,
            Email = email,
            FullName = normalizedFullName,
            IsActive = true,
            UserType = UserType.None
        };
    }

    public void InitializeCustomerProfile(string fullName, int age, string? phoneNumber)
    {
        var normalizedFullName = Guard.RequiredText(fullName, nameof(fullName));
        var validAge = Guard.Positive(age, nameof(age));
        var normalizedPhoneNumber = Guard.OptionalText(phoneNumber);

        FullName = normalizedFullName;
        Age = validAge;
        PhoneNumber = normalizedPhoneNumber;

        UserType |= UserType.Customer;
    }

    public void UpdateCustomerProfile(string fullName, int age, string? phoneNumber)
    {
        RequireCustomer();

        var normalizedFullName = Guard.RequiredText(fullName, nameof(fullName));
        var validAge = Guard.Positive(age, nameof(age));
        var normalizedPhoneNumber = Guard.OptionalText(phoneNumber);

        FullName = normalizedFullName;
        Age = validAge;
        PhoneNumber = normalizedPhoneNumber;
    }

    public void AddAddress(Address address, bool makeDefault = false)
    {
        RequireCustomer();
        ArgumentNullException.ThrowIfNull(address);

        var userAddress = new UserAddress(address, makeDefault);

        if (_addresses.Any(existingAddress => existingAddress.Details == address))
        {
            throw new DuplicateAddressException();
        }

        if (makeDefault)
        {
            MarkAllAddressesAsNonDefault();
        }

        _addresses.Add(userAddress);
    }

    public void RemoveAddress(int addressId)
    {
        RequireCustomer();
        _addresses.Remove(GetRequiredAddress(addressId));
    }

    public void SetDefaultAddress(int addressId)
    {
        RequireCustomer();
        var selectedAddress = GetRequiredAddress(addressId);

        MarkAllAddressesAsNonDefault();
        selectedAddress.MarkAsDefault();
    }

    public DeliveryAddressSnapshot CreateDeliveryAddressSnapshot(int addressId)
    {
        RequireCustomer();
        var address = GetRequiredAddress(addressId).Details;

        return new DeliveryAddressSnapshot(
            address.Street,
            address.City,
            address.BuildingNumber,
            address.Floor);
    }

    public void SubmitDeliveryAgentApplication(VehicleType vehicleType)
    {
        if (!Enum.IsDefined(vehicleType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(vehicleType),
                vehicleType,
                "Vehicle type is not supported.");
        }

        if (AgentApprovalStatus == AgentApproval.Approved)
        {
            throw new AgentApplicationNotPendingException();
        }

        VehicleType = vehicleType;
        AgentApprovalStatus = AgentApproval.PendingApproval;
    }

    public void ApproveDeliveryAgentApplication()
    {
        if (AgentApprovalStatus != AgentApproval.PendingApproval)
        {
            throw new AgentApplicationNotPendingException();
        }

        AgentApprovalStatus = AgentApproval.Approved;
        UserType |= UserType.DeliveryAgent;
        DeliveryAgentStatus = Users.DeliveryAgentStatus.Offline;
    }

    public void RejectDeliveryAgentApplication()
    {
        if (AgentApprovalStatus != AgentApproval.PendingApproval)
        {
            throw new AgentApplicationNotPendingException();
        }

        AgentApprovalStatus = AgentApproval.Rejected;
    }

    public bool IsAvailable()
    {
        RequireAgent();
        return DeliveryAgentStatus == Users.DeliveryAgentStatus.Available;
    }

    public void GoOnline()
    {
        RequireAgent();

        if (DeliveryAgentStatus is Users.DeliveryAgentStatus.Suspended or Users.DeliveryAgentStatus.Busy)
        {
            throw new AgentNotAvailableException();
        }

        DeliveryAgentStatus = Users.DeliveryAgentStatus.Available;
    }

    public void GoOffline()
    {
        RequireAgent();

        if (DeliveryAgentStatus == Users.DeliveryAgentStatus.Busy)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
                "A busy delivery agent cannot go offline.");
        }

        if (DeliveryAgentStatus == Users.DeliveryAgentStatus.Suspended)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
                "A suspended delivery agent cannot change online status.");
        }

        DeliveryAgentStatus = Users.DeliveryAgentStatus.Offline;
    }

    public void Suspend()
    {
        RequireAgent();

        if (DeliveryAgentStatus == Users.DeliveryAgentStatus.Busy)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
                "A busy delivery agent cannot be suspended.");
        }

        DeliveryAgentStatus = Users.DeliveryAgentStatus.Suspended;
    }

    internal void MarkBusy()
    {
        RequireAgent();

        if (!IsAvailable())
        {
            throw new AgentNotAvailableException();
        }

        DeliveryAgentStatus = Users.DeliveryAgentStatus.Busy;
    }

    internal void MarkAvailable()
    {
        RequireAgent();

        if (DeliveryAgentStatus != Users.DeliveryAgentStatus.Busy)
        {
            throw new InvalidDeliveryAgentStatusTransitionException(
                "Only a busy delivery agent can be released as available.");
        }

        DeliveryAgentStatus = Users.DeliveryAgentStatus.Available;
    }

    public void UpdateLocation(GeoLocation location)
    {
        RequireAgent();
        CurrentLocation = location ?? throw new ArgumentNullException(nameof(location));
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    // IAuditable
    public void SetCreatedAudit(DateTime createdAt, string? createdBy)
    {
        createdAt = Guard.Utc(createdAt, nameof(createdAt));

        if (CreatedAt == default)
        {
            CreatedAt = createdAt;
        }

        CreatedBy = Guard.OptionalText(createdBy);
    }

    public void SetModifiedAudit(DateTime modifiedAt, string? modifiedBy)
    {
        ModifiedAt = Guard.Utc(modifiedAt, nameof(modifiedAt));
        ModifiedBy = Guard.OptionalText(modifiedBy);
    }

    // ISoftDeletable
    public void SoftDelete(DateTime deletedAt, string? deletedBy)
    {
        if (IsDeleted)
        {
            return;
        }

        deletedAt = Guard.Utc(deletedAt, nameof(deletedAt));
        var normalizedDeletedBy = Guard.OptionalText(deletedBy);

        IsDeleted = true;
        DeletedAt = deletedAt;
        DeletedBy = normalizedDeletedBy;
        SetModifiedAudit(deletedAt, normalizedDeletedBy);
    }

    public void Restore(DateTime restoredAt, string? restoredBy)
    {
        if (!IsDeleted)
        {
            return;
        }

        restoredAt = Guard.Utc(restoredAt, nameof(restoredAt));
        var normalizedRestoredBy = Guard.OptionalText(restoredBy);

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        SetModifiedAudit(restoredAt, normalizedRestoredBy);
    }

    private void RequireCustomer()
    {
        if (!UserType.HasFlag(UserType.Customer))
        {
            throw new CustomerProfileNotInitializedException();
        }
    }

    private void RequireAgent()
    {
        if (DeliveryAgentStatus is null || !UserType.HasFlag(UserType.DeliveryAgent))
        {
            throw new DeliveryAgentNotInitializedException();
        }
    }

    private UserAddress GetRequiredAddress(int addressId)
    {
        Guard.Positive(addressId, nameof(addressId));

        return _addresses.SingleOrDefault(address => address.Id == addressId)
            ?? throw new AddressNotFoundException();
    }

    private void MarkAllAddressesAsNonDefault()
    {
        foreach (var address in _addresses)
        {
            address.MarkAsNonDefault();
        }
    }
}
