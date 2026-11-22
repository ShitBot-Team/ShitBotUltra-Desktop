# Windows Desktop + Core package

The packaging script produces one self-contained `artifacts\ShitBotUltra.Desktop.exe`. It contains the .NET and Windows App SDK runtime, `sbu.exe`, licenses and source notices. `sbu.exe` is a small Desktop-specific Go host linked with Core; it is not a separate CLI application that the user has to install or operate. Core continues to own configuration, databases and business logic. Do not rename the generated EXE: unpackaged WinUI uses its base name when resolving the project's XAML resource index.

The one-file release is a distribution convenience, not a zero-extraction runtime: Windows App SDK and .NET extract the embedded payload to the current user's temporary bundle directory before Desktop starts. This preserves the existing side-by-side `sbu.exe` contract without asking the user to unpack a ZIP manually.

## Build the package

On Windows, install .NET 10 SDK, Go 1.24 or newer and Git. The build also needs network access to GitHub, Go modules and NuGet. In the Desktop repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\pack-windows.ps1
```

The default fetches the current Core repository HEAD, compiles its runtime into the bundled host, publishes Desktop for x64, and creates `artifacts\ShitBotUltra.Desktop.exe`. To build ARM64, use `-Architecture arm64`. To use a specific Core branch, tag or commit, supply `-CoreRef <ref>`. To include local Core changes instead of downloading, supply `-CoreSource C:\path\to\ShitBotUltra`. The embedded `SOURCE-INFO.txt` records the Core commit and whether its working tree was dirty where Git metadata is available. The script stops on build failure and does not replace an existing EXE; use another `-OutputDirectory` if you need to keep multiple builds.

The downloaded Core source must expose `github.com/ShitBot-Team/ShitBotUltra/runtime` with the supported runtime API. Desktop and Core versions can change independently; confirm compatibility before distributing a build. The default host does not register the Core updater.

## Use the package

Run `ShitBotUltra.Desktop.exe` directly; no manual ZIP extraction is required. You may move it, but do not change its filename. Core configuration, database, secrets and runtime files are stored directly beside the launched EXE, as are `desktop.settings.json` and the `logs` directory. New settings start or attach to the local Core host automatically when the embedded host has been extracted. In Settings, click **Connect** with an empty token to use the Core-generated local administrator token; remote connections require their own token. Existing `%APPDATA%\ShitBotUltra` data is not moved or removed automatically.

Core still needs appropriate node, adapter and bot configuration. Image rendering may require configuring a compatible Chromium executable as required by Core. This EXE does not install Chromium, create shortcuts, provide an uninstaller, update Desktop automatically, or stop a separately attached Core. The release must be checked on Windows, including launch, connection and graceful shutdown, before distribution.
