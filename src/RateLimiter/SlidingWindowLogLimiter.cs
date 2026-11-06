namespace RateLimiter;

/// <summary>
/// Sliding window log: stores the timestamp of every accepted request in the last <c>window</c>.
/// Exact (no boundary bursts) at the cost of O(limit) memory.
/// </summary>
public sealed class SlidingWindowLogLimiter : IRateLimiter
{
    private readonly object _gate = new();
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;
    private readonly Queue<long> _hits = new(); // one entry per permit, oldest first

    public SlidingWindowLogLimiter(int limit, TimeSpan window, TimeProvider? timeProvider = null)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        _limit = limit;
        _window = window;
        _time = timeProvider ?? TimeProvider.System;
    }

    public RateLimitResult TryAcquire(int permits = 1)
    {
        if (permits <= 0) throw new ArgumentOutOfRangeException(nameof(permits));
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            Evict(now);
            if (permits > _limit) return RateLimitResult.Deny(_limit - _hits.Count, Timeout.InfiniteTimeSpan);
            if (_hits.Count + permits <= _limit)
            {
                for (var i = 0; i < permits; i++) _hits.Enqueue(now);
                return RateLimitResult.Allow(_limit - _hits.Count);
            }
            // Wait until enough of the oldest hits fall out of the window.
            var mustExpire = _hits.Count + permits - _limit;
            var pivot = _hits.ElementAt(mustExpire - 1);
            var wait = _window - _time.GetElapsedTime(pivot, now);
            return RateLimitResult.Deny(_limit - _hits.Count, wait < TimeSpan.Zero ? TimeSpan.Zero : wait);
        }
    }

    private void Evict(long now)
    {
        while (_hits.Count > 0 && _time.GetElapsedTime(_hits.Peek(), now) >= _window) _hits.Dequeue();
    }
}
