namespace shitbotteam.ultra.desktop.Models;

public sealed record DesktopSettings
{
    public string CoreEndpoint { get; init; } = "http://127.0.0.1:17880/";

    public string CliExecutablePath { get; init; } = DesktopPaths.BundledCoreHostPath;

    public const string DefaultCliArguments = "serve --no-webui";

    public string CliArguments { get; init; } = DefaultCliArguments;

    // Fresh installs start a bundled host when present; persisted preferences win.
    public bool StartCoreOnLaunch { get; init; } = File.Exists(DesktopPaths.BundledCoreHostPath);

    public bool MinimizeToTrayOnClose { get; init; }

    // Old settings files omit this field and follow the Windows UI language.
    public string Language { get; init; } = "system";
}

public static class DesktopPaths
{
    // AppContext.BaseDirectory is the .NET single-file extraction directory.
    // Environment.ProcessPath instead identifies the EXE the user launched.
    public static string ExecutableDirectory
    {
        get
        {
            var executablePath = Environment.ProcessPath;
            var directory = string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Path.GetDirectoryName(executablePath);
            return Path.GetFullPath(string.IsNullOrWhiteSpace(directory)
                ? AppContext.BaseDirectory
                : directory);
        }
    }

    public static string BundledCoreHostPath => Path.Combine(AppContext.BaseDirectory, "sbu.exe");
}

public static class CorePaths
{
    public static string GetDefaultConfigDirectory()
    {
        return DesktopPaths.ExecutableDirectory;
    }

    public static string GetAdminCredentialPath(string configDirectory)
    {
        return Path.Combine(configDirectory, "secrets", "admin.token");
    }
}
