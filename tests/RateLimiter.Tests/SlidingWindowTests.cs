namespace RateLimiter.Tests;

public class SlidingWindowLogTests
{
    [Fact]
    public void EnforcesLimitWithinWindow()
    {
        var time = new ManualTime();
        var l = new SlidingWindowLogLimiter(3, TimeSpan.FromSeconds(10), time);
        for (var i = 0; i < 3; i++) Assert.True(l.TryAcquire().Allowed);
        Assert.False(l.TryAcquire().Allowed);
    }

    [Fact]
    public void RetryAfter_PointsToOldestHitExpiry()
    {
        var time = new ManualTime();
        var l = new SlidingWindowLogLimiter(2, TimeSpan.FromSeconds(10), time);
        l.TryAcquire();
        time.Advance(TimeSpan.FromSeconds(4));
        l.TryAcquire();
        var r = l.TryAcquire();
        Assert.False(r.Allowed);
        Assert.Equal(TimeSpan.FromSeconds(6), r.RetryAfter);
    }

    [Fact]
    public void WindowSlides_NoBoundaryBurst()
    {
        var time = new ManualTime();
        var l = new SlidingWindowLogLimiter(2, TimeSpan.FromSeconds(10), time);
        time.Advance(TimeSpan.FromSeconds(9));
        l.TryAcquire(2);
        time.Advance(TimeSpan.FromSeconds(2)); // a fixed window would have reset here
        Assert.False(l.TryAcquire().Allowed);
        time.Advance(TimeSpan.FromSeconds(8));
        Assert.True(l.TryAcquire(2).Allowed);
    }

    [Fact]
    public void MultiplePermits_RetryAfterAccountsForNeeded()
    {
        var time = new ManualTime();
        var l = new SlidingWindowLogLimiter(3, TimeSpan.FromSeconds(10), time);
        l.TryAcquire();
        time.Advance(TimeSpan.FromSeconds(1));
        l.TryAcquire();
        time.Advance(TimeSpan.FromSeconds(1));
        l.TryAcquire();
        var r = l.TryAcquire(2); // needs the two oldest hits to expire
        Assert.False(r.Allowed);
        Assert.Equal(TimeSpan.FromSeconds(9), r.RetryAfter);
    }

    [Fact]
    public void TooManyPermits_Impossible() =>
        Assert.Equal(Timeout.InfiniteTimeSpan, new SlidingWindowLogLimiter(2, TimeSpan.FromSeconds(1), new ManualTime()).TryAcquire(3).RetryAfter);
}

public class SlidingWindowCounterTests
{
    [Fact]
    public void BasicLimit()
    {
        var time = new ManualTime();
        var l = new SlidingWindowCounterLimiter(4, TimeSpan.FromSeconds(10), time);
        for (var i = 0; i < 4; i++) Assert.True(l.TryAcquire().Allowed);
        Assert.False(l.TryAcquire().Allowed);
    }

    [Fact]
    public void PreviousWindow_IsWeightedByOverlap()
    {
        var time = new ManualTime();
        var l = new SlidingWindowCounterLimiter(10, TimeSpan.FromSeconds(10), time);
        l.TryAcquire(10); // all in window 0
        time.Advance(TimeSpan.FromSeconds(15)); // halfway into window 1: previous weighs 0.5 => 5 used
        Assert.True(l.TryAcquire(5).Allowed);
        Assert.False(l.TryAcquire().Allowed);
    }

    [Fact]
    public void AfterTwoIdleWindows_Resets()
    {
        var time = new ManualTime();
        var l = new SlidingWindowCounterLimiter(3, TimeSpan.FromSeconds(10), time);
        l.TryAcquire(3);
        time.Advance(TimeSpan.FromSeconds(25));
        Assert.True(l.TryAcquire(3).Allowed);
    }
}

public class KeyedRateLimiterTests
{
    [Fact]
    public void KeysAreIsolated()
    {
        var time = new ManualTime();
        var k = new KeyedRateLimiter<string>(() => new TokenBucketLimiter(1, 1, time));
        Assert.True(k.TryAcquire("alice").Allowed);
        Assert.False(k.TryAcquire("alice").Allowed);
        Assert.True(k.TryAcquire("bob").Allowed);
        Assert.Equal(2, k.KeyCount);
    }

    [Fact]
    public void Forget_ResetsKey()
    {
        var time = new ManualTime();
        var k = new KeyedRateLimiter<int>(() => new TokenBucketLimiter(1, 0.001, time));
        k.TryAcquire(1);
        Assert.False(k.TryAcquire(1).Allowed);
        Assert.True(k.Forget(1));
        Assert.True(k.TryAcquire(1).Allowed);
    }
}
