using System.Text.Json.Serialization;

namespace DisplayScalingManager.Core.State;

public sealed class PersistedState
{
    [JsonPropertyName("CurrentMode")]
    public OperatingMode CurrentMode { get; set; } = OperatingMode.Unknown;

    [JsonPropertyName("LastScale")]
    public int LastScale { get; set; }

    [JsonPropertyName("LastRun")]
    public DateTimeOffset LastRun { get; set; }
}
