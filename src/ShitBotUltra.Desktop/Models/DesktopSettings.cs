namespace shitbotteam.ultra.desktop.Models;

public sealed record DesktopSettings
{
    public string CoreEndpoint { get; init; } = "http://127.0.0.1:17880/";

    public string CliExecutablePath { get; init; } = Path.Combine(AppContext.BaseDirectory, "sbu.exe");

    public string CliArguments { get; init; } = string.Empty;

    public bool StartCoreOnLaunch { get; init; }

    public bool MinimizeToTrayOnClose { get; init; }
}

public static class CorePaths
{
    public static string GetDefaultConfigDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShitBotUltra");
    }

    public static string GetAdminCredentialPath(string configDirectory)
    {
        return Path.Combine(configDirectory, "secrets", "admin.token");
    }
}
