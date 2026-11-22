# Desktop implementation

## Technology and distribution

- C# / .NET 10, WinUI 3 and XAML.
- Windows App SDK 2.7.0 (stable channel), CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.DependencyInjection 10.0.0.
- Root namespace: `shitbotteam.ultra.desktop`.
- Minimum target: Windows 10 build 19041.
- The app is unpackaged (`WindowsPackageType=None`) because the planned installer and update handoff are owned by this repository. Publish profiles are provided for `win-x64` and `win-arm64`; both are .NET and Windows App SDK self-contained. Native Windows App SDK files remain side-by-side, so `PublishSingleFile` is intentionally disabled.
- The Windows packaging script builds the Desktop and a minimal Go host linked to Core, placing that host beside Desktop as `sbu.exe`. No separately installed CLI is needed. The executable path and arguments remain configurable for compatible hosts.

The package is an unpackaged, self-contained directory in a ZIP; users extract the complete directory and run the Desktop executable. It does not provide an installer, shortcuts, or automatic Desktop upgrades. See [Windows packaging](windows-package.md).

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
- Desktop starts a local Core host executable. The bundled host is called `sbu.exe` to retain the existing settings contract and accepts `serve --no-webui`. Settings permits an explicit override for a compatible host; arguments are passed directly without a shell.
- A graceful stop is sent only to the process started by this Desktop session. The attached instance is never stopped.
- Force Stop is a separate, explicit process-tree termination action.
- `StartCoreOnLaunch` starts or attaches during Desktop startup. It defaults to enabled for fresh settings when a bundled host exists; a previously saved value is preserved.
- An optional `MinimizeToTrayOnClose` preference keeps the window available from the Windows notification area. The tray Exit command closes Desktop; it does not terminate Core.
- Closing the Desktop window disconnects its event stream and releases its process handle; it deliberately leaves a managed Core process running.

The Core configuration directory follows Go's documented user configuration directory and is displayed as `%APPDATA%/ShitBotUltra` on Windows. Desktop does not accept a custom config directory until its host exposes a documented matching option; this avoids displaying a setting that the host would silently ignore.

## Current delivery boundary

The Desktop contains the shell and local host controls, configuration source and section editors, credentials, activity/events, plugin authorization and lifecycle, template editing and previews, whitelist import previews/commit/restore, task monitoring, and connected-host update controls. Every mutation is delegated to the Core API and uses Core's revision/ownership checks when provided. The update page requests updates of the connected Core host only, never replaces an attached service or claims that a handoff means success.

The bundled host links to Core's Go runtime directly and does not include a separate CLI product. Its `Updates` option is unset, so the connected-host updater is disabled in this bundle. A Desktop-specific updater, installer and rollback are not implemented.

Core currently takes `run/core.lock`, but the snapshot contains neither a host read-only validation command nor a documented Windows interoperability and offline transaction contract. Offline configuration editing remains disabled; Desktop does not invent an independent lock or infer that the directory is free from a failed HTTP probe.

The import interface retains exclusion selections when reading reports and requires a fresh unchanged ready preview before commit. Task request IDs are kept while this Desktop session remains open; after a restart consult the Core run history and verify any unknown result before another execution. The logger records Desktop lifecycle and Core management-event summaries, not host stdout/stderr. Credentials used for remote Core connections must be entered explicitly rather than read from the local default token file.

The Windows UI, host startup and graceful stop, packaging, and API compatibility require validation on Windows before a release is described as usable. Historical build output does not establish the result of a later build.
