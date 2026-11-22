# Windows Desktop + Core package

The release folder contains `ShitBotUltra.Desktop.exe`, its self-contained .NET and Windows App SDK files, and `sbu.exe`. The latter is a small Desktop-specific Go host linked with Core; it is not a separate CLI application that the user has to install or operate. Core continues to own configuration, databases and business logic.

## Build the package

On Windows, install .NET 10 SDK, Go 1.24 or newer and Git. The build also needs network access to GitHub, Go modules and NuGet. In the Desktop repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\pack-windows.ps1
```

The default fetches the current Core repository HEAD, compiles its runtime into the bundled host, publishes Desktop for x64, and creates a ZIP in `artifacts`. To build ARM64, use `-Architecture arm64`. To use a specific Core branch, tag or commit, supply `-CoreRef <ref>`. To include local Core changes instead of downloading, supply `-CoreSource C:\path\to\ShitBotUltra`. The ZIP records the Core commit and whether its working tree was dirty where Git metadata is available. The script stops on build failure and does not replace existing ZIPs.

The downloaded Core source must expose `github.com/ShitBot-Team/ShitBotUltra/runtime` with the supported runtime API. Desktop and Core versions can change independently; confirm compatibility before distributing a build. The default host does not register the Core updater.

## Use the package

Extract the whole ZIP to a folder and open `ShitBotUltra.Desktop.exe`. New settings start or attach to the local Core host automatically when the bundled host exists. In Settings, click **Connect** with an empty token to use the Core-generated local administrator token; remote connections require their own token. Existing saved settings retain their startup choice and any customized host path. Core data is stored under `%APPDATA%\ShitBotUltra` and remains when the application folder is replaced.

Core still needs appropriate node, adapter and bot configuration. Image rendering may require configuring a compatible Chromium executable as required by Core. This ZIP does not install Chromium, update Desktop automatically, or stop a separately attached Core. The release must be checked on Windows, including launch, connection and graceful shutdown, before distribution.
