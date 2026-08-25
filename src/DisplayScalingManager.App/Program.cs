using DisplayScalingManager.Core.Configuration;
using DisplayScalingManager.Core.Detection;
using DisplayScalingManager.Core.Events;
using DisplayScalingManager.Core.Logging;
using DisplayScalingManager.Core.Orchestration;
using DisplayScalingManager.Core.Scaling;
using DisplayScalingManager.Core.State;
using Serilog;

namespace DisplayScalingManager.App;

internal static class Program {
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var logger = LoggerSetup.CreateLogger();
        Log.Logger = logger;

        Application.ThreadException += (_, e) =>
            logger.Error(e.Exception, "Unhandled exception on the UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Error(e.ExceptionObject as Exception, "Unhandled exception on a background thread");

        logger.Information("DisplayScalingManager starting up");

        var config = AppConfig.LoadOrCreateDefault(logger: logger);
        var detector = new DisplayTopologyDetector(logger);
        var stateManager = new StateManager(logger: logger);
        var scalingController = new ScalingController(new DisplayConfigDpiScalingStrategy(logger), logger);
        var controller = new ApplicationController(detector, stateManager, scalingController, config, logger);

        using var eventMonitor = new EventMonitor(config.DebounceMilliseconds, logger);
        eventMonitor.Evaluate += () => _ = SafeEvaluateAsync(controller, logger);
        eventMonitor.Start();

        using var trayContext = new TrayIconContext(controller, eventMonitor, logger);

        // Cover the "just launched at logon" case explicitly, since SessionSwitch may not fire
        // for the process that is itself the logon-triggered artifact.
        _ = SafeEvaluateAsync(controller, logger);

        Application.Run(trayContext);

        logger.Information("DisplayScalingManager shutting down");
        Log.CloseAndFlush();
    }

    private static async Task SafeEvaluateAsync(ApplicationController controller, ILogger logger)
    {
        try
        {
            await controller.EvaluateAndApply().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unhandled exception during EvaluateAndApply");
        }
    }
}
