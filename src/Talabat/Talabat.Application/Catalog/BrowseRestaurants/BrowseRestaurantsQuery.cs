using Talabat.Application.Common.Paging;

namespace Talabat.Application.Catalog.BrowseRestaurants;

public sealed record BrowseRestaurantsQuery(
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize);
