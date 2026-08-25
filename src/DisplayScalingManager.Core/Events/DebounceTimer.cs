namespace DisplayScalingManager.Core.Events;

/// <summary>
/// Coalesces a burst of rapid calls into a single invocation of <paramref name="onFire"/>,
/// firing only after <paramref name="dueTimeMilliseconds"/> of quiet since the last <see cref="Ping"/>.
/// </summary>

public sealed class DebounceTimer : IDisposable
{
    private readonly Timer _timer;
    private readonly int _dueTimeMilliseconds;

    public DebounceTimer(int dueTimeMilliseconds, Action onFire)
    {
        _dueTimeMilliseconds = dueTimeMilliseconds;
        _timer = new Timer(_ => onFire(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Ping() => _timer.Change(_dueTimeMilliseconds, Timeout.Infinite);

    public void Dispose() => _timer.Dispose();

}
