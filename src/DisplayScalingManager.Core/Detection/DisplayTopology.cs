namespace DisplayScalingManager.Core.Detection;

public sealed record DisplayTopology(IReadOnlyList<DisplayInfo> Displays, bool IsValid)
{
    public static DisplayTopology Invalid { get; } = new(Array.Empty<DisplayInfo>(), IsValid: false);

    public bool HasPhysicalExternal => Displays.Any(d => d.Classification == DisplayClassification.PhysicalExternal);
}
