using Scrobblint.Application.Abstractions;

namespace Scrobblint.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
