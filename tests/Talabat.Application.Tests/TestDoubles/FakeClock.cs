using Talabat.Application.Abstractions;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);
}
