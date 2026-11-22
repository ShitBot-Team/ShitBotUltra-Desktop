using System.Text.Json;
using shitbotteam.ultra.desktop.Models;

namespace shitbotteam.ultra.desktop.Services;

public sealed class DesktopSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShitBotUltra",
        "desktop.settings.json");

    public async Task<DesktopSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new DesktopSettings();
        }

        try
        {
            await using var stream = new FileStream(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var settings = await JsonSerializer.DeserializeAsync<DesktopSettings>(
                stream,
                JsonOptions,
                cancellationToken) ?? new DesktopSettings();

            return settings with
            {
                CoreEndpoint = string.IsNullOrWhiteSpace(settings.CoreEndpoint)
                    ? new DesktopSettings().CoreEndpoint
                    : settings.CoreEndpoint,
                CliExecutablePath = string.IsNullOrWhiteSpace(settings.CliExecutablePath)
                    ? new DesktopSettings().CliExecutablePath
                    : settings.CliExecutablePath,
                CliArguments = string.IsNullOrWhiteSpace(settings.CliArguments)
                    ? DesktopSettings.DefaultCliArguments
                    : settings.CliArguments
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Desktop settings are invalid JSON. The original file was left unchanged.", exception);
        }
    }

    public async Task SaveAsync(DesktopSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

        var temporaryPath = string.Concat(_settingsPath, ".", Guid.NewGuid().ToString("N"), ".tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true,
                options: FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
