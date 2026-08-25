# Architecture Notes

## DPI scaling mechanism (living risk note)

As of Windows 11 24H2 / .NET 8, there is no fully public, Microsoft-documented API to set the
OS-wide "Change the size of text, apps, and other items" scaling percentage. Two undocumented
mechanisms are known to work:

1. `SystemParametersInfo(SPI_SETLOGICALDPIOVERRIDE, ...)` — only affects the primary/single
   monitor, can't target a specific display in a multi-monitor setup, and has reported bugs
   (e.g. requesting 100% landing on 125%). **Not used.**
2. `DisplayConfigGetDeviceInfo` / `DisplayConfigSetDeviceInfo` with undocumented negative
   `DISPLAYCONFIG_DEVICE_INFO_TYPE` values `-3` (get) and `-4` (set), and the associated
   `DISPLAYCONFIG_SOURCE_DPI_SCALE_GET`/`_SET` structs. **This is what
   `DisplayConfigDpiScalingStrategy` uses.**

Why (2) over (1) or registry editing:
- Both functions are public, exported, documented Win32 APIs (`user32.dll`) — only the `type`
  value and struct layout are undocumented, not the calling convention itself.
- It reuses the same `adapterId`/`sourceId` pairs already produced by `QueryDisplayConfig` for
  topology detection (`DisplayTopologyDetector`), so there's no separate registry-subkey-to-monitor
  correlation step to get wrong.
- It's genuinely per-source, matching Windows' actual DPI model, so multi-monitor handling
  (`ScalingController` iterating every active source) is natural rather than a workaround.
- Values are relative to the OS's own "recommended" scaling for that source, resolved through the
  known scaling ladder `{100,125,150,175,200,225,250,300,350,400,450,500}` — no guessing what an
  index means in percent.
- Takes effect immediately; no `WM_SETTINGCHANGE` broadcast, `rundll32` shell-out, or logoff
  needed — it's the same call path the Settings app itself uses.
- No elevation required (matches manually dragging the scaling slider).

### Residual risk

The negative type codes and struct layouts are reverse-engineered (credit:
`lihas/windows-DPI-scaling-sample`, built on by `imniko/SetDPI`), not published by Microsoft, and
could change in a future Windows release. `DisplayConfigDpiScalingStrategy` guards this with a
`Marshal.SizeOf` check against the expected struct sizes (`0x20` for the get struct, `0x18` for the
set struct) and logs a warning if a future OS build doesn't match — a canary, not a fix. Because
the mechanism sits entirely behind `IScalingStrategy`, a future replacement (should Microsoft ship
a supported API, or break this one) only requires a new implementation of that interface —
`ScalingController`, `ApplicationController`, and everything above it are unaffected.

Resolution-based approaches (changing display resolution instead of DPI scaling) were considered
and rejected: they render at a lower pixel count and let the monitor upscale the framebuffer,
which is visibly blurry on any panel not being driven at an exact integer-scaled resolution —
unlike DPI scaling, which keeps native resolution and only scales UI chrome/text.

## Hosting model

Not a Windows Service: Session 0 isolation means a service process cannot reliably reach the
interactive user's session state, and `Microsoft.Win32.SystemEvents.SessionSwitch` /
`DisplaySettingsChanged` require a Win32 message pump running inside the user's own window
station (`WinSta0`) to receive `WM_WTSSESSION_CHANGE` / `WM_DISPLAYCHANGE`. Instead, the app is a
normal executable launched via a Task Scheduler "At log on" trigger (`Install-Startup.ps1`),
running at normal user rights with a hidden `ApplicationContext` + tray `NotifyIcon` providing the
message pump `EventMonitor` subscribes on.
