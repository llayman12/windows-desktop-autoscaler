using Microsoft.Win32;
using Serilog;

namespace DisplayScalingManager.Core.Events;

/// <summary>
/// Subscribes to session/display OS events and raises a single debounced <see cref="Evaluate"/>
/// callback, coalescing bursts (e.g. a dock connect fires several DisplaySettingsChanged events).
/// No polling is involved — the process is idle until Windows raises one of these events.
/// </summary>

public sealed class EventMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly DebounceTimer _debounce;
    private bool _started;

    public event Action? Evaluate;

    public EventMonitor(int debounceMilliseconds, ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
        _debounce = new DebounceTimer(debounceMilliseconds, FireEvaluate);
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _started = true;
        _logger.Information("EventMonitor: subscribed to SessionSwitch and DisplaySettingsChanged");
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _started = false;
        _logger.Information("EventMonitor: unsubscribed from OS events");
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
        {
            _logger.Debug("EventMonitor: trigger from SessionSwitch ({Reason})", e.Reason);
            _debounce.Ping();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _logger.Debug("EventMonitor: trigger from DisplaySettingsChanged");
        _debounce.Ping();
    }

    private void FireEvaluate()
    {
        try
        {
            Evaluate?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "EventMonitor: unhandled exception in Evaluate callback");
        }
    }

    public void Dispose()
    {
        Stop();
        _debounce.Dispose();
    }
}
