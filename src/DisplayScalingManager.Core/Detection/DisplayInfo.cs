namespace DisplayScalingManager.Core.Detection;

/// <summary>
/// Identifies a display's source adapter/id pair — the same identity DPI scaling is set against,
/// since Windows treats DPI scaling as a property of the source, not the target.
/// </summary>
public readonly record struct DisplaySourceId(uint AdapterIdLowPart, int AdapterIdHighPart, uint SourceId);

public sealed record DisplayInfo(
    DisplaySourceId Source,
    string? DeviceName,
    DisplayConfigVideoOutputTechnology Technology,
    DisplayClassification Classification);

