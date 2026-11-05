namespace Memshare.Tests.Support;

/// <summary>A clock stopped at a known time.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 11, 5, 10, 30, 0, TimeSpan.Zero);

    public FixedTime()
        : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
