using System.Security.Claims;
using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Delivery.API.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserCapabilityResolver _resolver;
    private bool _resolved;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, ICurrentUserCapabilityResolver resolver)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
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

        var userType = _resolver.GetUserTypeAsync(parsedId).GetAwaiter().GetResult();

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
