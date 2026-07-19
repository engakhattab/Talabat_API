using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Identity;

public static class UnifiedUserIdentityServiceCollectionExtensions
{
    public static IdentityBuilder AddUnifiedUserIdentityCore(this IServiceCollection services)
    {
        return services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<TalabatDbContext>();
    }
}
