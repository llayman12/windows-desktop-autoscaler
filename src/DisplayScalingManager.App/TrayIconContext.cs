using System.Diagnostics;
using DisplayScalingManager.Core.Configuration;
using DisplayScalingManager.Core.Events;
using DisplayScalingManager.Core.Orchestration;
using Serilog;

namespace DisplayScalingManager.App;

/// <summary>
/// Hosts the app as a tray-only presence (no main window) showing the current mode/scale,
/// with a menu for manual re-evaluation, opening the log folder, and exiting.
/// </summary>
internal sealed class TrayIconContext : ApplicationContext
{
    private readonly ApplicationController _controller;
    private readonly EventMonitor _eventMonitor;
    private readonly ILogger _logger;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;

    public TrayIconContext(ApplicationController controller, EventMonitor eventMonitor, ILogger logger)
    {
        _controller = controller;
        _eventMonitor = eventMonitor;
        _logger = logger;

        _statusItem = new ToolStripMenuItem("Mode: (evaluating…)") { Enabled = false };
        var menu = new ContextMenuStrip();

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Evaluate now", null, (_, _) => _ = EvaluateNowAsync());
        menu.Items.Add("Open logs", null, (_, _) => OpenLogsFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Display Scaling Manager",
            ContextMenuStrip = menu,
            Visible = true,
        };

        RefreshStatus();
    }

    private async Task EvaluateNowAsync()
    {
        try
        {
            await _controller.EvaluateAndApply().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "TrayIconContext: manual evaluation failed");
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void RefreshStatus() => _statusItem.Text = $"Mode: {_controller.CurrentMode}";

    private static void OpenLogsFolder()
    {
        AppPaths.EnsureDirectoriesExist();
        Process.Start(new ProcessStartInfo(AppPaths.LogsDirectory) { UseShellExecute = true });
    }

    protected override void ExitThreadCore()
    {
        _eventMonitor.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
