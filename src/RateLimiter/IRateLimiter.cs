namespace RateLimiter;

/// <summary>Outcome of a rate limit decision.</summary>
/// <param name="Allowed">Whether the request may proceed.</param>
/// <param name="Remaining">Approximate permits left after this decision.</param>
/// <param name="RetryAfter">When denied, how long to wait before enough permits are available.</param>
public readonly record struct RateLimitResult(bool Allowed, double Remaining, TimeSpan RetryAfter)
{
    public static RateLimitResult Allow(double remaining) => new(true, remaining, TimeSpan.Zero);
    public static RateLimitResult Deny(double remaining, TimeSpan retryAfter) => new(false, remaining, retryAfter);
}

public interface IRateLimiter
{
    /// <summary>Tries to consume <paramref name="permits"/> permits.</summary>
    RateLimitResult TryAcquire(int permits = 1);
}
