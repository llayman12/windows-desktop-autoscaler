using System.Runtime.InteropServices;
using DisplayScalingManager.Core.Detection;
using Serilog;

namespace DisplayScalingManager.Core.Scaling;

/// <summary>
/// Sets per-source DPI scaling via the undocumented negative DISPLAYCONFIG_DEVICE_INFO_TYPE
/// values -3 (get) and -4 (set), layered on top of the same public, documented
/// DisplayConfigGetDeviceInfo/DisplayConfigSetDeviceInfo Win32 functions used for topology
/// detection. This is reverse-engineered behavior (not published by Microsoft) — see
/// docs/ARCHITECTURE.md for the risk this carries and why it's isolated behind
/// <see cref="IScalingStrategy"/>. Credit: reverse-engineering by the community
/// (lihas/windows-DPI-scaling-sample, imniko/SetDPI).
/// </summary>
public sealed class DisplayConfigDpiScalingStrategy : IScalingStrategy
{
    // Windows' known DPI scaling ladder — the recommended/current/min/max values reported by the
    // OS are indices relative to this list, not literal percentages.
    private static readonly int[] DpiSteps = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

    private const int DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE = -3;
    private const int DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE = -4;

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public int minScaleRel;
        public int curScaleRel;
        public int maxScaleRel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public int scaleRel;
    }

    private readonly ILogger _logger;

    public DisplayConfigDpiScalingStrategy(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;

        // Canary against a future Windows release changing this reverse-engineered layout.
        var getSize = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>();
        var setSize = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>();
        if (getSize != 0x20 || setSize != 0x18)
        {
            _logger.Warning(
                "DisplayConfigDpiScalingStrategy: unexpected struct size (get={GetSize}, set={SetSize}); " +
                "DPI scaling calls may fail or behave unexpectedly on this Windows build",
                getSize, setSize);
        }
    }

    public Task<int?> GetScalingPercentAsync(DisplaySourceId source)
    {
        var info = TryGetDpiScaleInfo(source, out var dpiInfo);
        return Task.FromResult(info ? (int?)dpiInfo.Current : null);
    }

    public Task<bool> SetScalingPercentAsync(DisplaySourceId source, int percent)
    {
        if (!TryGetDpiScaleInfo(source, out var dpiInfo))
        {
            _logger.Error("DisplayConfigDpiScalingStrategy: could not read current DPI scale info for source {Source}", source);
            return Task.FromResult(false);
        }

        if (percent == dpiInfo.Current)
        {
            return Task.FromResult(true);
        }

        var clamped = Math.Clamp(percent, dpiInfo.Minimum, dpiInfo.Maximum);
        if (clamped != percent)
        {
            _logger.Warning(
                "DisplayConfigDpiScalingStrategy: requested {Requested}% out of supported range [{Min}-{Max}] for source {Source}; clamping to {Clamped}%",
                percent, dpiInfo.Minimum, dpiInfo.Maximum, source, clamped);
        }

        var targetIndex = Array.IndexOf(DpiSteps, clamped);
        var recommendedIndex = Array.IndexOf(DpiSteps, dpiInfo.Recommended);
        if (targetIndex < 0 || recommendedIndex < 0)
        {
            _logger.Error(
                "DisplayConfigDpiScalingStrategy: could not map percent to a known DPI step (target={Target}, recommended={Recommended})",
                clamped, dpiInfo.Recommended);
            return Task.FromResult(false);
        }

        var setPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            header = MakeHeader(DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE, source, Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>()),
            scaleRel = targetIndex - recommendedIndex,
        };

        var status = NativeMethods.DisplayConfigSetDeviceInfo(ref setPacket.header);
        if (status != NativeMethods.ERROR_SUCCESS)
        {
            _logger.Error("DisplayConfigDpiScalingStrategy: DisplayConfigSetDeviceInfo failed with status {Status} for source {Source}", status, source);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private readonly record struct DpiInfo(int Minimum, int Current, int Maximum, int Recommended);

    private bool TryGetDpiScaleInfo(DisplaySourceId source, out DpiInfo dpiInfo)
    {
        dpiInfo = default;

        var getPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            header = MakeHeader(DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE, source, Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>()),
        };

        var status = NativeMethods.DisplayConfigGetDeviceInfo(ref getPacket.header);
        if (status != NativeMethods.ERROR_SUCCESS)
        {
            _logger.Error("DisplayConfigDpiScalingStrategy: DisplayConfigGetDeviceInfo failed with status {Status} for source {Source}", status, source);
            return false;
        }

        var minRel = Math.Clamp(getPacket.minScaleRel, -DpiSteps.Length + 1, DpiSteps.Length - 1);
        var maxRel = Math.Clamp(getPacket.maxScaleRel, -DpiSteps.Length + 1, DpiSteps.Length - 1);
        var curRel = Math.Clamp(getPacket.curScaleRel, minRel, maxRel);

        var minAbs = Math.Abs(minRel);
        if (minAbs + maxRel + 1 > DpiSteps.Length || minAbs + maxRel < 0)
        {
            _logger.Error(
                "DisplayConfigDpiScalingStrategy: DPI step range from OS doesn't fit known ladder (min={Min}, max={Max}); ladder may be outdated for this hardware",
                getPacket.minScaleRel, getPacket.maxScaleRel);
            return false;
        }

        dpiInfo = new DpiInfo(
            Minimum: DpiSteps[minAbs + minRel],
            Current: DpiSteps[minAbs + curRel],
            Maximum: DpiSteps[minAbs + maxRel],
            Recommended: DpiSteps[minAbs]);
        return true;
    }

    private static DISPLAYCONFIG_DEVICE_INFO_HEADER MakeHeader(int type, DisplaySourceId source, int packetSize) => new()
    {
        type = type,
        size = (uint)packetSize,
        adapterId = new LUID { LowPart = source.AdapterIdLowPart, HighPart = source.AdapterIdHighPart },
        id = source.SourceId,
    };
}
