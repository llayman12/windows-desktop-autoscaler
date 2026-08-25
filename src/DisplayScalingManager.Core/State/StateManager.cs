using System.Text.Json;
using DisplayScalingManager.Core.Configuration;
using DisplayScalingManager.Core.Detection;
using Serilog;

namespace DisplayScalingManager.Core.State;

public sealed class StateManager
{
    private readonly string _statePath;
    private readonly ILogger _logger;
    private PersistedState _current;

    public StateManager(string? statePath = null, ILogger? logger = null)
    {
        _statePath = statePath ?? AppPaths.StateFilePath;
        _logger = logger ?? Log.Logger;
        _current = Load(_statePath, _logger);
    }

    public OperatingMode CurrentMode => _current.CurrentMode;

    public static OperatingMode DetermineMode(DisplayTopology topology) =>
        topology.HasPhysicalExternal ? OperatingMode.Desktop : OperatingMode.Portable;

    public bool StateChanged(OperatingMode newMode) => newMode != _current.CurrentMode;

    public void Persist(OperatingMode mode, int scalePercent)
    {
        _current = new PersistedState
        {
            CurrentMode = mode,
            LastScale = scalePercent,
            LastRun = DateTimeOffset.UtcNow,
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var tempPath = _statePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "StateManager: failed to persist state to {Path}", _statePath);
        }
    }

    private static PersistedState Load(string path, ILogger logger)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<PersistedState>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "StateManager: failed to load {Path}, starting from Unknown state", path);
        }

        return new PersistedState { CurrentMode = OperatingMode.Unknown };
    }
}
