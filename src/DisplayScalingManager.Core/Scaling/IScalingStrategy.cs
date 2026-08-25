using DisplayScalingManager.Core.Detection;

namespace DisplayScalingManager.Core.Scaling;

/// <summary>
/// Abstraction boundary around the mechanism used to read/apply DPI scaling for a display source.
/// Windows exposes no fully public, documented API for this today (see docs/ARCHITECTURE.md) — this
/// seam exists so the concrete mechanism can be swapped without touching orchestration code.
/// </summary>
public interface IScalingStrategy
{
    Task<int?> GetScalingPercentAsync(DisplaySourceId source);

    Task<bool> SetScalingPercentAsync(DisplaySourceId source, int percent);
}
