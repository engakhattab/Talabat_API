using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TalabatDbContext _dbContext;
    private bool _resolved;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, TalabatDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public bool IsAuthenticated
    {
        get
        {
            EnsureResolved();
            return _isAuthenticated;
        }
    }

    public int? UserId
    {
        get
        {
            EnsureResolved();
            return _userId;
        }
    }

    public bool HasCustomerCapability
    {
        get
        {
            EnsureResolved();
            return _hasCustomerCapability;
        }
    }

    public int? CustomerId
    {
        get
        {
            EnsureResolved();
            return _customerId;
        }
    }

    public bool HasDeliveryAgentCapability
    {
        get
        {
            EnsureResolved();
            return _hasDeliveryAgentCapability;
        }
    }

    public int? AgentId
    {
        get
        {
            EnsureResolved();
            return _agentId;
        }
    }

    private bool _isAuthenticated;
    private int? _userId;
    private bool _hasCustomerCapability;
    private int? _customerId;
    private bool _hasDeliveryAgentCapability;
    private int? _agentId;

    private void EnsureResolved()
    {
        if (_resolved)
        {
            return;
        }

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            _isAuthenticated = false;
            _resolved = true;
            return;
        }

        var subjectValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (!int.TryParse(subjectValue, out var parsedId) || parsedId <= 0)
        {
            _isAuthenticated = true;
            _userId = null;
            _resolved = true;
            return;
        }

        _isAuthenticated = true;
        _userId = parsedId;

        var userType = _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == parsedId)
            .Select(u => u.UserType)
            .FirstOrDefault();

        if (userType.HasFlag(UserType.Customer))
        {
            _hasCustomerCapability = true;
            _customerId = parsedId;
        }

        if (userType.HasFlag(UserType.DeliveryAgent))
        {
            _hasDeliveryAgentCapability = true;
            _agentId = parsedId;
        }

        _resolved = true;
    }
}
