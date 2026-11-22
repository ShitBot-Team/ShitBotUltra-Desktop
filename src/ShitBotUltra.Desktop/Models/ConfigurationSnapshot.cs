namespace shitbotteam.ultra.desktop.Models;

public sealed record ConfigurationSnapshot(
    CoreConfigurationDocument Document,
    string ETag);
