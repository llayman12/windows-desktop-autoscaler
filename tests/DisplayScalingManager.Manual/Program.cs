using DisplayScalingManager.Core.Configuration;
using DisplayScalingManager.Core.Detection;
using DisplayScalingManager.Core.Logging;
using DisplayScalingManager.Core.Orchestration;
using DisplayScalingManager.Core.Scaling;
using DisplayScalingManager.Core.State;
using Serilog;

var logger = LoggerSetup.CreateLogger();
Log.Logger = logger;

var detector = new DisplayTopologyDetector(logger);

Console.WriteLine("Display Scaling Manager — manual test harness");
Console.WriteLine("Commands: [t]opology, [e]valuate, [s]tate, [q]uit");

var config = AppConfig.LoadOrCreateDefault(logger: logger);
var stateManager = new StateManager(logger: logger);
var scalingController = new ScalingController(new DisplayConfigDpiScalingStrategy(logger), logger);
var controller = new ApplicationController(detector, stateManager, scalingController, config, logger);

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine()?.Trim().ToLowerInvariant();
    switch (input)
    {
        case "t":
        case "topology":
            DumpTopology(detector);
            break;

        case "e":
        case "evaluate":
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await controller.EvaluateAndApply();
            sw.Stop();
            Console.WriteLine($"EvaluateAndApply completed in {sw.ElapsedMilliseconds} ms. CurrentMode={controller.CurrentMode}");
            break;

        case "s":
        case "state":
            Console.WriteLine(File.Exists(AppPaths.StateFilePath)
                ? File.ReadAllText(AppPaths.StateFilePath)
                : "(no state.json yet)");
            break;

        case "q":
        case "quit":
        case "exit":
            return;

        default:
            Console.WriteLine("Unknown command. Use t/e/s/q.");
            break;
    }
}

static void DumpTopology(DisplayTopologyDetector detector)
{
    var topology = detector.GetCurrentTopology();
    Console.WriteLine($"IsValid={topology.IsValid}  HasPhysicalExternal={topology.HasPhysicalExternal}  Count={topology.Displays.Count}");
    foreach (var d in topology.Displays)
    {
        Console.WriteLine($"  - {d.DeviceName ?? "(unnamed)"}: Technology={d.Technology}, Classification={d.Classification}, Source={d.Source}");
    }
}
