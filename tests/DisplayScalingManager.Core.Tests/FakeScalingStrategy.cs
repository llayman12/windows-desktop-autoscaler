using DisplayScalingManager.Core.Detection;
using DisplayScalingManager.Core.Scaling;

namespace DisplayScalingManager.Core.Tests;

/// <summary>Stateful in-memory stand-in for IScalingStrategy, since ScalingController reads back
/// after writing to verify — a static mock return value can't satisfy that round trip.</summary>
internal sealed class FakeScalingStrategy : IScalingStrategy
{
    private readonly Dictionary<DisplaySourceId, int> _current = new();
    public int SetCallCount { get; private set; }
    public bool FailSets { get; set; }

    public FakeScalingStrategy(int initialPercent) => _initialPercent = initialPercent;

    private readonly int _initialPercent;

    public Task<int?> GetScalingPercentAsync(DisplaySourceId source)
    {
        if (!_current.TryGetValue(source, out var percent))
        {
            percent = _initialPercent;
        }
        return Task.FromResult<int?>(percent);
    }

    public Task<bool> SetScalingPercentAsync(DisplaySourceId source, int percent)
    {
        SetCallCount++;
        if (FailSets)
        {
            return Task.FromResult(false);
        }
        _current[source] = percent;
        return Task.FromResult(true);
    }
}
