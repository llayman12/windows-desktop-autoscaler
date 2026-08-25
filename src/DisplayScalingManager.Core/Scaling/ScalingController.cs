using DisplayScalingManager.Core.Detection;
using Serilog;

namespace DisplayScalingManager.Core.Scaling;

/// <summary>
/// Applies one target scaling percent across every currently-active display source, wrapping
/// the underlying <see cref="IScalingStrategy"/> with pre-checks, verification, and logging.
/// </summary>
public sealed class ScalingController
{
    private readonly IScalingStrategy _strategy;
    private readonly ILogger _logger;

    public ScalingController(IScalingStrategy strategy, ILogger? logger = null)
    {
        _strategy = strategy;
        _logger = logger ?? Log.Logger;
    }

    public async Task<ScalingResult> SetScalingAsync(DisplayTopology topology, int percent)
    {
        if (topology.Displays.Count == 0)
        {
            return ScalingResult.Empty;
        }

        var results = new List<SourceScalingResult>();
        foreach (var display in topology.Displays)
        {
            var previous = await _strategy.GetScalingPercentAsync(display.Source).ConfigureAwait(false);
            if (previous == percent)
            {
                results.Add(new SourceScalingResult(display.Source, Success: true, previous));
                continue;
            }

            var applied = await _strategy.SetScalingPercentAsync(display.Source, percent).ConfigureAwait(false);
            if (!applied)
            {
                _logger.Error("ScalingController: failed to set {Percent}% on source {Source}", percent, display.Source);
                results.Add(new SourceScalingResult(display.Source, Success: false, previous));
                continue;
            }

            var verified = await _strategy.GetScalingPercentAsync(display.Source).ConfigureAwait(false);
            var success = verified == percent;
            if (!success)
            {
                _logger.Error(
                    "ScalingController: verification failed for source {Source} — expected {Expected}%, read back {Actual}%",
                    display.Source, percent, verified);
            }
            else
            {
                _logger.Information("ScalingController: source {Source} scaling changed {Previous}% -> {Percent}%", display.Source, previous, percent);
            }

            results.Add(new SourceScalingResult(display.Source, success, previous));
        }

        return new ScalingResult(results.All(r => r.Success), results);
    }
}
