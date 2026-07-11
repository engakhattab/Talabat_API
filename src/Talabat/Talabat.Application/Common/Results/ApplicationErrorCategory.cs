namespace Talabat.Application.Common.Results;

public enum ApplicationErrorCategory
{
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unavailable = 4,
    OwnershipMismatch = 5
}
