using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Tests.UserDomain.Users;

public class UserAccountStateTests
{
    [Fact]
    public void Register_ShouldAssignUserNameAndEmail()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.Equal("john_doe", user.UserName);
        Assert.Equal("john@example.com", user.Email);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("  john_doe  ", "  john@example.com  ")]
    public void Register_ShouldDeferUserNameAndEmailPolicyValidation(
        string userName,
        string email)
    {
        var user = User.Register(userName, email, "John Doe");

        Assert.Equal(userName, user.UserName);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public void Register_ShouldAssignFullName()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.Equal("John Doe", user.FullName);
    }

    [Fact]
    public void Register_ShouldThrowOnNullFullName()
    {
        var act = () => User.Register("john_doe", "john@example.com", null!);

        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("fullName", ex.ParamName);
    }

    [Fact]
    public void Register_ShouldThrowOnBlankFullName()
    {
        var act = () => User.Register("john_doe", "john@example.com", "   ");

        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("fullName", ex.ParamName);
    }

    [Fact]
    public void Register_ShouldSetIsActiveTrue()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.True(user.IsActive);
    }

    [Fact]
    public void Register_ShouldSetUserTypeNone()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.Equal(UserType.None, user.UserType);
    }

    [Fact]
    public void Register_ShouldInitializeRowVersionEmpty()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.NotNull(user.RowVersion);
        Assert.Empty(user.RowVersion);
    }

    [Fact]
    public void Register_ShouldSetDefaultId()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.Equal(0, user.Id);
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_ShouldPreserveExistingState()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, "123456");

        user.Deactivate();
        user.Activate();

        Assert.Equal(UserType.Customer, user.UserType);
        Assert.Equal("John Doe", user.FullName);
        Assert.Equal(25, user.Age);
    }

    [Fact]
    public void SetCreatedAudit_ShouldSetCreatedAtWhenDefault()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        var now = DateTime.UtcNow;

        user.SetCreatedAudit(now, "system");

        Assert.Equal(now, user.CreatedAt);
        Assert.Equal("system", user.CreatedBy);
    }

    [Fact]
    public void SetCreatedAudit_ShouldNotOverwriteCreatedAt()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        var first = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        user.SetCreatedAudit(first, "first");
        user.SetCreatedAudit(second, "second");

        Assert.Equal(first, user.CreatedAt);
        Assert.Equal("second", user.CreatedBy);
    }

    [Fact]
    public void SetModifiedAudit_ShouldSetModifiedFields()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        var now = DateTime.UtcNow;

        user.SetModifiedAudit(now, "editor");

        Assert.Equal(now, user.ModifiedAt);
        Assert.Equal("editor", user.ModifiedBy);
    }

    [Fact]
    public void SoftDelete_ShouldMarkDeleted()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        var now = DateTime.UtcNow;

        user.SoftDelete(now, "admin");

        Assert.True(user.IsDeleted);
        Assert.Equal(now, user.DeletedAt);
        Assert.Equal("admin", user.DeletedBy);
    }

    [Fact]
    public void SoftDelete_ShouldBeIdempotent()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        var first = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        user.SoftDelete(first, "admin");
        user.SoftDelete(second, "other");

        Assert.Equal(first, user.DeletedAt);
        Assert.Equal("admin", user.DeletedBy);
    }

    [Fact]
    public void Restore_ShouldClearDeleteMetadata()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.SoftDelete(DateTime.UtcNow, "admin");

        var restoreTime = DateTime.UtcNow;
        user.Restore(restoreTime, "admin2");

        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);
        Assert.Null(user.DeletedBy);
        Assert.Equal(restoreTime, user.ModifiedAt);
    }

    [Fact]
    public void Restore_ShouldBeIdempotent()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.SoftDelete(DateTime.UtcNow, "admin");

        var restoreTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        user.Restore(restoreTime, "admin2");
        user.Restore(DateTime.UtcNow, "admin3");

        Assert.False(user.IsDeleted);
        Assert.Equal(restoreTime, user.ModifiedAt);
    }

    [Fact]
    public void Register_ShouldHaveNullOptionalFields()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.Null(user.Age);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.VehicleType);
        Assert.Null(user.DeliveryAgentStatus);
        Assert.Null(user.CurrentLocation);
        Assert.Null(user.AgentApprovalStatus);
    }

    [Fact]
    public void Register_ShouldHaveEmptyAddresses()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.NotNull(user.Addresses);
        Assert.Empty(user.Addresses);
    }
}
