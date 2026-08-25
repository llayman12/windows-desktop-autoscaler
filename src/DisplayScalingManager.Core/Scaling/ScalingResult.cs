using DisplayScalingManager.Core.Detection;

namespace DisplayScalingManager.Core.Scaling;

public sealed record SourceScalingResult(DisplaySourceId Source, bool Success, int? PreviousPercent);

public sealed record ScalingResult(bool Success, IReadOnlyList<SourceScalingResult> PerSourceResults)
{
    public static ScalingResult Empty { get; } = new(true, Array.Empty<SourceScalingResult>());
}
