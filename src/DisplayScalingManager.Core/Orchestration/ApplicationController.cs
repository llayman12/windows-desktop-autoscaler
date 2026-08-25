using DisplayScalingManager.Core.Configuration;
using DisplayScalingManager.Core.Detection;
using DisplayScalingManager.Core.Scaling;
using DisplayScalingManager.Core.State;
using Serilog;

namespace DisplayScalingManager.Core.Orchestration;

public sealed class ApplicationController
{
    private readonly IDisplayTopologyProvider _detector;
    private readonly StateManager _stateManager;
    private readonly ScalingController _scalingController;
    private readonly AppConfig _config;
    private readonly ILogger _logger;

    public ApplicationController(
        IDisplayTopologyProvider detector,
        StateManager stateManager,
        ScalingController scalingController,
        AppConfig config,
        ILogger? logger = null)
    {
        _detector = detector;
        _stateManager = stateManager;
        _scalingController = scalingController;
        _config = config;
        _logger = logger ?? Log.Logger;
    }

    /// <summary>Current mode/scale, for UI surfaces like the tray icon.</summary>
    public OperatingMode CurrentMode => _stateManager.CurrentMode;

    public async Task EvaluateAndApply()
    {
        var topology = _detector.GetCurrentTopology();
        if (!topology.IsValid)
        {
            _logger.Error("ApplicationController: display detection failed — leaving scaling untouched");
            return;
        }

        var newMode = StateManager.DetermineMode(topology);
        if (!_stateManager.StateChanged(newMode))
        {
            _logger.Debug("ApplicationController: no-op, already in {Mode}", newMode);
            return;
        }

        var targetPercent = newMode == OperatingMode.Desktop ? _config.DesktopPercent : _config.PortablePercent;

        var result = await _scalingController.SetScalingAsync(topology, targetPercent).ConfigureAwait(false);
        if (result.Success)
        {
            _stateManager.Persist(newMode, targetPercent);
            _logger.Information("ApplicationController: state transition -> {Mode} at {Percent}%", newMode, targetPercent);
        }
        else
        {
            _logger.Error("ApplicationController: scaling change to {Mode}/{Percent}% failed; will retry on next event", newMode, targetPercent);
        }
    }
}
