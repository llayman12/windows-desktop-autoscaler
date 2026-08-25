# windows-desktop-autoscaler

Lightweight Windows utility that automatically switches your display scaling between two
profiles based on whether a physically connected external monitor is present:

| Configuration | Scaling |
|---|---|
| Laptop panel only (or + wireless/virtual displays) | 100% |
| One or more physically connected external displays (HDMI, DisplayPort, DVI, VGA, USB-C Alt Mode, dock) | 125% |

Wireless (Miracast) and virtual displays never trigger a change. It's purely event-driven — no
polling — reacting to logon, unlock, and display configuration changes in under a second.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the technical design, including the DPI
scaling mechanism and why it was chosen over the alternatives.

## Project layout

```
src/
├── DisplayScalingManager.Core/     # detection, scaling, state, events, orchestration — no UI deps
├── DisplayScalingManager.App/      # tray-icon host, launched at logon
└── DisplayScalingManager.Installer/# PowerShell scripts to register/unregister the logon task
tests/
├── DisplayScalingManager.Core.Tests/ # xUnit unit tests
└── DisplayScalingManager.Manual/     # console harness for live hardware verification
docs/
└── ARCHITECTURE.md
```

## Building

Requires the .NET 8 SDK.

```powershell
dotnet build DisplayScalingManager.sln
```

## Running

```powershell
dotnet run --project src\DisplayScalingManager.App
```

This launches the app as a tray icon (no main window). Right-click it for **Evaluate now**,
**Open logs**, and **Exit**. State is persisted to `%LOCALAPPDATA%\DisplayScalingManager\state.json`,
configuration to `config.json` in the same folder (created with defaults on first run), and logs
to the `Logs` subfolder.

## Testing

```powershell
dotnet test tests\DisplayScalingManager.Core.Tests
```

For manual acceptance testing against real hardware (the scenarios in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)), use the console harness:

```powershell
dotnet run --project tests\DisplayScalingManager.Manual
```

Commands: `t` (dump current display topology), `e` (run one evaluate/apply cycle), `s` (print
`state.json`), `q` (quit).

## Installing at logon

Publish a self-contained single-file executable, then register it to launch at logon via
Task Scheduler:

```powershell
dotnet publish src\DisplayScalingManager.App -c Release
.\src\DisplayScalingManager.Installer\Install-Startup.ps1 -ExePath "src\DisplayScalingManager.App\bin\Release\net8.0-windows\win-x64\publish\DisplayScalingManager.App.exe"
```

To remove it:

```powershell
.\src\DisplayScalingManager.Installer\Uninstall-Startup.ps1
```

Add `-RemoveData` to also delete `%LOCALAPPDATA%\DisplayScalingManager`.

## Configuration

Edit `%LOCALAPPDATA%\DisplayScalingManager\config.json`:

```json
{
  "PortablePercent": 100,
  "DesktopPercent": 125,
  "DebounceMilliseconds": 500
}
```
