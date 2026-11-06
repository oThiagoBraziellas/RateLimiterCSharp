using System.Collections.Concurrent;

namespace RateLimiter;

/// <summary>Maintains one limiter per key (user id, IP, API key...) created lazily by a factory.</summary>
public sealed class KeyedRateLimiter<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, IRateLimiter> _limiters = new();
    private readonly Func<IRateLimiter> _factory;

    public KeyedRateLimiter(Func<IRateLimiter> factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public int KeyCount => _limiters.Count;

    public RateLimitResult TryAcquire(TKey key, int permits = 1) =>
        _limiters.GetOrAdd(key, _ => _factory()).TryAcquire(permits);

    /// <summary>Drops the state of a key (e.g. when the session ends) to bound memory.</summary>
    public bool Forget(TKey key) => _limiters.TryRemove(key, out _);
}
