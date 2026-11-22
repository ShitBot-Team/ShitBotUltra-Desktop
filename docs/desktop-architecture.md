# Desktop implementation

## Technology and distribution

- C# / .NET 10, WinUI 3 and XAML.
- Windows App SDK 2.7.0 (stable channel), CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.DependencyInjection 10.0.0.
- Root namespace: `shitbotteam.ultra.desktop`.
- Minimum target: Windows 10 build 19041.
- The app is unpackaged (`WindowsPackageType=None`) because the planned installer and update handoff are owned by this repository. Publish profiles are provided for `win-x64` and `win-arm64`; both are .NET and Windows App SDK self-contained. Native Windows App SDK files remain side-by-side, so `PublishSingleFile` is intentionally disabled.
- The release layout must place the matching `sbu.exe` beside the Desktop app, or the user can set its full path in Settings. The CLI binary is not fabricated or downloaded by the app project. CLI arguments are configurable because the CLI repository and its current command-line contract were not part of this source snapshot.

The project has not been built or run in this delivery. The publish profiles describe the intended release shape; they are not build evidence. Unpackaged distribution requires the app's custom installer to handle installation, shortcuts, upgrades and removal.

## Source layout

```text
src/ShitBotUltra.Desktop/
  App.xaml(.cs)                 Application composition root and DI registrations
  MainWindow.xaml(.cs)          Navigation shell only
  Models/                       Core DTOs and desktop preference records
  Services/                     Core HTTP/SSE, process ownership, settings and dispatch
  ViewModels/                   Presentation state and commands
  Views/Pages/                  Small XAML pages with no business rules
  Properties/PublishProfiles/   Architecture-specific self-contained publish settings
```

Views depend on ViewModels. ViewModels depend on service abstractions and Core contracts, not on the database or platform business logic. Core remains the only owner of robot connections, bindings, player data and business persistence.

## Core connection

The default Core URL is `http://127.0.0.1:17880/`. Non-loopback endpoints must use HTTPS. Every management request uses the Bearer token; the token is held in memory and is never written to `desktop.settings.json`. If the default Core credential file exists, Settings can read `%APPDATA%/ShitBotUltra/secrets/admin.token` for the current connection attempt.

The initial shell uses these management routes:

- `GET /api/v1/status`
- `GET /api/v1/dashboard` (optional on older Core builds)
- `GET /api/v1/connections`
- `GET /api/v1/config` and `PUT /api/v1/config` with the returned ETag in `If-Match`
- `PUT /api/v1/webui` to toggle the current session's WebUI without changing the persisted default
- `GET /api/v1/events` as an authenticated SSE stream with `Last-Event-ID` reconnect support

Core validates and persists configuration, creates backups and reports whether a restart is required. Desktop keeps the user's draft on a revision conflict and does not write Core files directly.

## Process ownership

- `Start / Attach` first probes the configured API. A reachable instance is attached, not claimed.
- Desktop starts only a local `sbu.exe`. The argument string is explicitly configured in Settings and passed directly to the executable without a shell; Desktop does not invent or hardcode a CLI command contract.
- A graceful stop is sent only to the process started by this Desktop session. The attached instance is never stopped.
- Force Stop is a separate, explicit process-tree termination action.
- An optional `StartCoreOnLaunch` preference starts or attaches during Desktop startup.
- Closing the Desktop window disconnects its event stream and releases its process handle; it deliberately leaves a managed Core process running.

The Core configuration directory follows Go's documented user configuration directory and is displayed as `%APPDATA%/ShitBotUltra` on Windows. Desktop does not accept a custom config directory until the CLI exposes a documented matching option; this avoids displaying a setting that the host would silently ignore.

## Current delivery boundary

The first implementation slices include the app shell, dashboard, connection summary, configuration source editor, event stream, local preferences, Core attachment and owned-process lifecycle, optional Core startup on Desktop launch, configurable CLI arguments and current-session WebUI control. Tray integration, offline configuration locking, log-file capture, update installation, plugin/template management and import workflows remain separate follow-up slices. No tests, build, publish or runtime checks were run.
