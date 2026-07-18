using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Talabat.Application.Abstractions;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Customer.API.Auth;

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

    public string IdentityUserId
    {
        get
        {
            EnsureResolved();
            return _identityUserId ?? string.Empty;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            EnsureResolved();
            return _isAuthenticated;
        }
    }

    public bool HasProfile
    {
        get
        {
            EnsureResolved();
            return _hasProfile;
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

    private string? _identityUserId;
    private bool _isAuthenticated;
    private bool _hasProfile;
    private int? _customerId;

    private void EnsureResolved()
    {
        if (_resolved)
        {
            return;
        }

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is null || !user.Identity?.IsAuthenticated == true)
        {
            _isAuthenticated = false;
            _identityUserId = string.Empty;
            _resolved = true;
            return;
        }

        _isAuthenticated = true;
        _identityUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? string.Empty;

        if (!string.IsNullOrEmpty(_identityUserId))
        {
            var customer = _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefault(c => c.IdentityUserId == _identityUserId);

            if (customer is not null)
            {
                _hasProfile = true;
                _customerId = customer.Id;
            }
        }

        _resolved = true;
    }
}
