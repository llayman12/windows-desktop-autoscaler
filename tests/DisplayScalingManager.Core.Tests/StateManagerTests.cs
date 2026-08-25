using DisplayScalingManager.Core.Detection;
using DisplayScalingManager.Core.State;
using Serilog;
using Xunit;

namespace DisplayScalingManager.Core.Tests;

public class StateManagerTests
{
    private static readonly ILogger NullLogger = new LoggerConfiguration().CreateLogger();

    private static DisplayInfo MakeDisplay(DisplayClassification classification) =>
        new(new DisplaySourceId(0, 0, 0), "Test", DisplayConfigVideoOutputTechnology.Hdmi, classification);

    [Fact]
    public void DetermineMode_ReturnsDesktop_WhenPhysicalExternalPresent()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.Internal), MakeDisplay(DisplayClassification.PhysicalExternal) },
            IsValid: true);

        Assert.Equal(OperatingMode.Desktop, StateManager.DetermineMode(topology));
    }

    [Fact]
    public void DetermineMode_ReturnsPortable_WhenOnlyInternalAndWirelessPresent()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.Internal), MakeDisplay(DisplayClassification.Wireless) },
            IsValid: true);

        Assert.Equal(OperatingMode.Portable, StateManager.DetermineMode(topology));
    }

    [Fact]
    public void DetermineMode_ReturnsPortable_WhenOnlyVirtualPresent()
    {
        var topology = new DisplayTopology(
            new[] { MakeDisplay(DisplayClassification.Internal), MakeDisplay(DisplayClassification.Virtual) },
            IsValid: true);

        Assert.Equal(OperatingMode.Portable, StateManager.DetermineMode(topology));
    }

    [Fact]
    public void StateChanged_IsFalse_WhenModeMatchesPersistedState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dsm-test-{Guid.NewGuid():N}.json");
        try
        {
            var manager = new StateManager(path, NullLogger);
            manager.Persist(OperatingMode.Desktop, 125);

            Assert.False(manager.StateChanged(OperatingMode.Desktop));
            Assert.True(manager.StateChanged(OperatingMode.Portable));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StateChanged_IsTrue_OnFirstRunWithNoPersistedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dsm-test-{Guid.NewGuid():N}.json");
        var manager = new StateManager(path, NullLogger);

        Assert.True(manager.StateChanged(OperatingMode.Portable));
    }
}
