using DisplayScalingManager.Core.Events;
using Xunit;

namespace DisplayScalingManager.Core.Tests;

public class DebounceTimerTests
{
    [Fact]
    public async Task Ping_CoalescesBurst_IntoSingleFire()
    {
        var fireCount = 0;
        using var debounce = new DebounceTimer(dueTimeMilliseconds: 300, onFire: () => Interlocked.Increment(ref fireCount));

        for (var i = 0; i < 5; i++)
        {
            debounce.Ping();
            await Task.Delay(20);
        }

        await Task.Delay(600);

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public async Task Ping_FiresAgain_AfterQuietPeriodBetweenBursts()
    {
        var fireCount = 0;
        using var debounce = new DebounceTimer(dueTimeMilliseconds: 100, onFire: () => Interlocked.Increment(ref fireCount));

        debounce.Ping();
        await Task.Delay(400);

        debounce.Ping();
        await Task.Delay(400);

        Assert.Equal(2, fireCount);
    }
}
