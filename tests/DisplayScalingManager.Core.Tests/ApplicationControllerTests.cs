using DisplayScalingManager.Core.Configuration;
using DisplayScalingManager.Core.Detection;
using DisplayScalingManager.Core.Orchestration;
using DisplayScalingManager.Core.Scaling;
using DisplayScalingManager.Core.State;
using Moq;
using Serilog;
using Xunit;

namespace DisplayScalingManager.Core.Tests;

public class ApplicationControllerTests
{
    private static readonly ILogger NullLogger = new LoggerConfiguration().CreateLogger();

    private static DisplayInfo MakeDisplay(DisplayClassification classification, uint sourceId = 0) =>
        new(new DisplaySourceId(0, 0, sourceId), "Test", DisplayConfigVideoOutputTechnology.Hdmi, classification);

    private static (ApplicationController Controller, string StatePath) BuildController(
        DisplayTopology topology, IScalingStrategy strategy, AppConfig? config = null)
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"dsm-test-{Guid.NewGuid():N}.json");
        var stateManager = new StateManager(statePath, NullLogger);
        var scalingController = new ScalingController(strategy, NullLogger);

        var detectorMock = new Mock<IDisplayTopologyProvider>();
        detectorMock.Setup(d => d.GetCurrentTopology()).Returns(topology);

        config ??= new AppConfig();
        var controller = new ApplicationController(detectorMock.Object, stateManager, scalingController, config, NullLogger);
        return (controller, statePath);
    }

    [Fact]
    public async Task EvaluateAndApply_DoesNothing_WhenDetectionFails()
    {
        var strategyMock = new Mock<IScalingStrategy>(MockBehavior.Strict);
        var (controller, statePath) = BuildController(DisplayTopology.Invalid, strategyMock.Object);
        try
        {
            await controller.EvaluateAndApply();

            Assert.Equal(OperatingMode.Unknown, controller.CurrentMode);
            Assert.False(File.Exists(statePath));
            strategyMock.VerifyNoOtherCalls();
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public async Task EvaluateAndApply_PersistsDesktopMode_WhenPhysicalExternalPresentAndScalingSucceeds()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.Internal, 0), MakeDisplay(DisplayClassification.PhysicalExternal, 1) },
            IsValid: true);

        var strategy = new FakeScalingStrategy(initialPercent: 100);
        var (controller, statePath) = BuildController(topology, strategy);
        try
        {
            await controller.EvaluateAndApply();

            Assert.Equal(OperatingMode.Desktop, controller.CurrentMode);
            Assert.True(File.Exists(statePath));
            Assert.Equal(2, strategy.SetCallCount); // one per active source
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public async Task EvaluateAndApply_StaysPortable_WhenOnlyWirelessAndInternalPresent()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.Internal, 0), MakeDisplay(DisplayClassification.Wireless, 1) },
            IsValid: true);

        var strategy = new FakeScalingStrategy(initialPercent: 125);
        var (controller, statePath) = BuildController(topology, strategy);
        try
        {
            await controller.EvaluateAndApply();

            Assert.Equal(OperatingMode.Portable, controller.CurrentMode);
            Assert.Equal(2, strategy.SetCallCount);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public async Task EvaluateAndApply_DoesNotPersist_WhenScalingFails()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.PhysicalExternal) },
            IsValid: true);

        var strategy = new FakeScalingStrategy(initialPercent: 100) { FailSets = true };
        var (controller, statePath) = BuildController(topology, strategy);
        try
        {
            await controller.EvaluateAndApply();

            Assert.Equal(OperatingMode.Unknown, controller.CurrentMode);
            Assert.False(File.Exists(statePath));
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public async Task EvaluateAndApply_IsNoOp_OnRepeatedCallsWithUnchangedTopology()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.PhysicalExternal) },
            IsValid: true);

        var strategy = new FakeScalingStrategy(initialPercent: 100);
        var (controller, statePath) = BuildController(topology, strategy);
        try
        {
            await controller.EvaluateAndApply();
            await controller.EvaluateAndApply();
            await controller.EvaluateAndApply();

            // SetScalingPercentAsync should only be invoked for the first (state-changing) call.
            Assert.Equal(1, strategy.SetCallCount);
        }
        finally
        {
            File.Delete(statePath);
        }
    }
}
