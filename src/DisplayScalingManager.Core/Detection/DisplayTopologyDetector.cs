using Serilog;

namespace DisplayScalingManager.Core.Detection;

public sealed class DisplayTopologyDetector : IDisplayTopologyProvider
{
    private readonly ILogger _logger;

    public DisplayTopologyDetector(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public bool HasPhysicalExternalDisplays() => GetCurrentTopology().HasPhysicalExternal;

    public DisplayTopology GetCurrentTopology()
    {
        try
        {
            if (!TryQueryActivePaths(out var paths))
            {
                _logger.Error("DisplayTopologyDetector: failed to query active display paths");
                return DisplayTopology.Invalid;
            }

            var displays = new List<DisplayInfo>();
            foreach (var path in paths)
            {
                if ((path.flags & NativeMethods.DISPLAYCONFIG_PATH_ACTIVE) == 0)
                {
                    continue;
                }

                var technology = path.targetInfo.outputTechnology;
                var classification = Classify(technology);
                if (classification == DisplayClassification.Unknown)
                {
                    _logger.Warning(
                        "DisplayTopologyDetector: unrecognized output technology {Technology} — classifying as Unknown/not-physical-external",
                        technology);
                }

                var source = new DisplaySourceId(
                    path.sourceInfo.adapterId.LowPart,
                    path.sourceInfo.adapterId.HighPart,
                    path.sourceInfo.id);

                var deviceName = TryGetTargetFriendlyName(path.targetInfo.adapterId, path.targetInfo.id);

                displays.Add(new DisplayInfo(source, deviceName, technology, classification));
            }

            var topology = new DisplayTopology(displays, IsValid: true);
            _logger.Information(
                "DisplayTopologyDetector: detected {Count} active display(s), HasPhysicalExternal={HasExternal}",
                topology.Displays.Count, topology.HasPhysicalExternal);
            return topology;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DisplayTopologyDetector: unexpected failure querying display topology");
            return DisplayTopology.Invalid;
        }
    }

    private static DisplayClassification Classify(DisplayConfigVideoOutputTechnology technology) => technology switch
    {
        DisplayConfigVideoOutputTechnology.Internal => DisplayClassification.Internal,
        DisplayConfigVideoOutputTechnology.DisplayPortEmbedded => DisplayClassification.Internal,
        DisplayConfigVideoOutputTechnology.UdiEmbedded => DisplayClassification.Internal,

        DisplayConfigVideoOutputTechnology.Hdmi => DisplayClassification.PhysicalExternal,
        DisplayConfigVideoOutputTechnology.Dvi => DisplayClassification.PhysicalExternal,
        DisplayConfigVideoOutputTechnology.Hd15 => DisplayClassification.PhysicalExternal,
        DisplayConfigVideoOutputTechnology.DisplayPortExternal => DisplayClassification.PhysicalExternal,
        DisplayConfigVideoOutputTechnology.DisplayPortUsbTunnel => DisplayClassification.PhysicalExternal,
        DisplayConfigVideoOutputTechnology.UdiExternal => DisplayClassification.PhysicalExternal,

        DisplayConfigVideoOutputTechnology.Miracast => DisplayClassification.Wireless,

        DisplayConfigVideoOutputTechnology.IndirectWired => DisplayClassification.Virtual,
        DisplayConfigVideoOutputTechnology.IndirectVirtual => DisplayClassification.Virtual,

        _ => DisplayClassification.Unknown,
    };

    private static bool TryQueryActivePaths(out DISPLAYCONFIG_PATH_INFO[] paths)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var status = NativeMethods.GetDisplayConfigBufferSizes(
                QDC.QDC_ONLY_ACTIVE_PATHS, out var numPaths, out var numModes);
            if (status != NativeMethods.ERROR_SUCCESS)
            {
                paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
                return false;
            }

            var pathArray = new DISPLAYCONFIG_PATH_INFO[numPaths];
            var modeArray = new DISPLAYCONFIG_MODE_INFO[numModes];
            for (var i = 0; i < modeArray.Length; i++)
            {
                modeArray[i].modeInfoUnion = new byte[48];
            }

            status = NativeMethods.QueryDisplayConfig(
                QDC.QDC_ONLY_ACTIVE_PATHS, ref numPaths, pathArray, ref numModes, modeArray, IntPtr.Zero);

            if (status == NativeMethods.ERROR_SUCCESS)
            {
                paths = pathArray;
                return true;
            }

            if (status != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
            {
                paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
                return false;
            }
            // Topology changed between sizing and query calls (documented race) — retry once.
        }

        paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
        return false;
    }

    private static string? TryGetTargetFriendlyName(LUID adapterId, uint targetId)
    {
        var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = (int)DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                adapterId = adapterId,
                id = targetId,
            },
        };

        var status = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
        if (status != NativeMethods.ERROR_SUCCESS)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(request.monitorFriendlyDeviceName) ? null : request.monitorFriendlyDeviceName;
    }
}
