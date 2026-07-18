using Talabat.Application.Abstractions;

namespace Talabat.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
