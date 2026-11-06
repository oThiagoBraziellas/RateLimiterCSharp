namespace RateLimiter;

/// <summary>
/// Token bucket: holds up to <c>capacity</c> tokens and refills continuously at <c>refillPerSecond</c>.
/// Allows bursts up to the capacity while enforcing the long-run average rate. O(1) time and memory.
/// </summary>
public sealed class TokenBucketLimiter : IRateLimiter
{
    private readonly object _gate = new();
    private readonly double _capacity;
    private readonly double _refillPerSecond;
    private readonly TimeProvider _time;
    private double _tokens;
    private long _lastTimestamp;

    public TokenBucketLimiter(int capacity, double refillPerSecond, TimeProvider? timeProvider = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (refillPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillPerSecond));
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
        _time = timeProvider ?? TimeProvider.System;
        _tokens = capacity;
        _lastTimestamp = _time.GetTimestamp();
    }

    public RateLimitResult TryAcquire(int permits = 1)
    {
        if (permits <= 0) throw new ArgumentOutOfRangeException(nameof(permits));
        lock (_gate)
        {
            Refill();
            if (permits > _capacity)
                return RateLimitResult.Deny(_tokens, Timeout.InfiniteTimeSpan); // can never succeed
            if (_tokens >= permits)
            {
                _tokens -= permits;
                return RateLimitResult.Allow(_tokens);
            }
            var missing = permits - _tokens;
            return RateLimitResult.Deny(_tokens, TimeSpan.FromSeconds(missing / _refillPerSecond));
        }
    }

    private void Refill()
    {
        var now = _time.GetTimestamp();
        var elapsed = _time.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        _lastTimestamp = now;
        if (elapsed > 0) _tokens = Math.Min(_capacity, _tokens + elapsed * _refillPerSecond);
    }
}
