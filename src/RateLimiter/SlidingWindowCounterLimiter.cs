namespace RateLimiter;

/// <summary>
/// Sliding window counter: keeps only two fixed-window counters and weights the previous one by how much of it
/// still overlaps the sliding window. O(1) memory with an approximation error bounded by the traffic shape.
/// </summary>
public sealed class SlidingWindowCounterLimiter : IRateLimiter
{
    private readonly object _gate = new();
    private readonly int _limit;
    private readonly long _windowTicks;
    private readonly TimeProvider _time;
    private readonly long _origin;
    private long _currentIndex;
    private int _currentCount;
    private int _previousCount;

    public SlidingWindowCounterLimiter(int limit, TimeSpan window, TimeProvider? timeProvider = null)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        _limit = limit;
        _time = timeProvider ?? TimeProvider.System;
        _windowTicks = (long)(window.TotalSeconds * _time.TimestampFrequency);
        _origin = _time.GetTimestamp();
    }

    public RateLimitResult TryAcquire(int permits = 1)
    {
        if (permits <= 0) throw new ArgumentOutOfRangeException(nameof(permits));
        lock (_gate)
        {
            var elapsed = _time.GetTimestamp() - _origin;
            var index = elapsed / _windowTicks;
            Roll(index);
            var fractionIntoWindow = (double)(elapsed % _windowTicks) / _windowTicks;
            var estimated = _previousCount * (1 - fractionIntoWindow) + _currentCount;
            if (permits <= _limit && estimated + permits <= _limit)
            {
                _currentCount += permits;
                return RateLimitResult.Allow(_limit - (estimated + permits));
            }
            // Retry hint: at worst when the current fixed window ends.
            var untilNextWindow = (1 - fractionIntoWindow) * _windowTicks / _time.TimestampFrequency;
            return RateLimitResult.Deny(Math.Max(0, _limit - estimated),
                permits > _limit ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(untilNextWindow));
        }
    }

    private void Roll(long index)
    {
        if (index == _currentIndex) return;
        _previousCount = index == _currentIndex + 1 ? _currentCount : 0;
        _currentCount = 0;
        _currentIndex = index;
    }
}
