namespace RateLimiter.Tests;

public class TokenBucketTests
{
    [Fact]
    public void AllowsBurstUpToCapacity_ThenDenies()
    {
        var time = new ManualTime();
        var l = new TokenBucketLimiter(5, 1, time);
        for (var i = 0; i < 5; i++) Assert.True(l.TryAcquire().Allowed);
        var denied = l.TryAcquire();
        Assert.False(denied.Allowed);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter);
    }

    [Fact]
    public void RefillsOverTime()
    {
        var time = new ManualTime();
        var l = new TokenBucketLimiter(2, 2, time); // 2 tokens/s
        l.TryAcquire(2);
        Assert.False(l.TryAcquire().Allowed);
        time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(l.TryAcquire().Allowed);
        Assert.False(l.TryAcquire().Allowed);
    }

    [Fact]
    public void Refill_IsCappedAtCapacity()
    {
        var time = new ManualTime();
        var l = new TokenBucketLimiter(3, 10, time);
        l.TryAcquire(3);
        time.Advance(TimeSpan.FromHours(1));
        Assert.True(l.TryAcquire(3).Allowed);
        Assert.False(l.TryAcquire().Allowed);
    }

    [Fact]
    public void RequestLargerThanCapacity_NeverSucceeds()
    {
        var l = new TokenBucketLimiter(3, 1, new ManualTime());
        var r = l.TryAcquire(4);
        Assert.False(r.Allowed);
        Assert.Equal(Timeout.InfiniteTimeSpan, r.RetryAfter);
    }

    [Fact]
    public void FractionalRefill_Accumulates()
    {
        var time = new ManualTime();
        var l = new TokenBucketLimiter(1, 1, time);
        l.TryAcquire();
        for (var i = 0; i < 10; i++) { time.Advance(TimeSpan.FromMilliseconds(100)); }
        Assert.True(l.TryAcquire().Allowed);
    }

    [Fact]
    public void InvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketLimiter(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketLimiter(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketLimiter(1, 1).TryAcquire(0));
    }

    [Fact]
    public async Task Concurrent_NeverGrantsMoreThanCapacity()
    {
        var l = new TokenBucketLimiter(100, 0.0001); // real clock, negligible refill
        var granted = 0;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                if (l.TryAcquire().Allowed) Interlocked.Increment(ref granted);
        })));
        Assert.Equal(100, granted);
    }
}
