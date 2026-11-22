using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Models;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DesktopSettingsStore _settingsStore;
    private readonly MainViewModel _mainViewModel;
    private string _savedEndpoint = string.Empty;
    private string _savedCliExecutablePath = string.Empty;
    private string _savedCliArguments = string.Empty;
    private bool _savedStartCoreOnLaunch;

    [ObservableProperty]
    private string endpoint = "http://127.0.0.1:17880/";

    [ObservableProperty]
    private string cliExecutablePath = Path.Combine(AppContext.BaseDirectory, "sbu.exe");

    [ObservableProperty]
    private string cliArguments = string.Empty;

    [ObservableProperty]
    private bool startCoreOnLaunch;

    [ObservableProperty]
    private string tokenInput = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    public string ConfigDirectory => CorePaths.GetDefaultConfigDirectory();

    public bool IsDirty => HasLoaded
        && (!string.Equals(Endpoint, _savedEndpoint, StringComparison.Ordinal)
            || !string.Equals(CliExecutablePath, _savedCliExecutablePath, StringComparison.Ordinal)
            || !string.Equals(CliArguments, _savedCliArguments, StringComparison.Ordinal)
            || StartCoreOnLaunch != _savedStartCoreOnLaunch);

    public SettingsViewModel(DesktopSettingsStore settingsStore, MainViewModel mainViewModel)
    {
        _settingsStore = settingsStore;
        _mainViewModel = mainViewModel;
    }

    partial void OnEndpointChanged(string value) => OnPropertyChanged(nameof(IsDirty));

    partial void OnCliExecutablePathChanged(string value) => OnPropertyChanged(nameof(IsDirty));

    partial void OnCliArgumentsChanged(string value) => OnPropertyChanged(nameof(IsDirty));

    partial void OnStartCoreOnLaunchChanged(bool value) => OnPropertyChanged(nameof(IsDirty));

    partial void OnHasLoadedChanged(bool value) => OnPropertyChanged(nameof(IsDirty));

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsDirty)
        {
            StatusMessage = "Unsaved preferences are open. Save them or explicitly discard them before reloading.";
            return;
        }

        await LoadCoreAsync(cancellationToken);
    }

    public async Task ReloadDiscardAsync(CancellationToken cancellationToken = default)
    {
        await LoadCoreAsync(cancellationToken);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            ApplySavedSettings(settings);
            StatusMessage = "Credentials are never written to Desktop settings.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var settings = CreateSettings();
            await _settingsStore.SaveAsync(settings);
            ApplySavedSettings(settings);
            StatusMessage = "Desktop preferences saved.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var settings = CreateSettings();
            await _settingsStore.SaveAsync(settings);
            ApplySavedSettings(settings);

            var token = string.IsNullOrWhiteSpace(TokenInput)
                ? await CoreCredentialReader.TryReadAdminTokenAsync(ConfigDirectory)
                : TokenInput;
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusMessage = "Enter an admin token or create secrets/admin.token in the Core configuration directory.";
                return;
            }

            await _mainViewModel.ConnectAsync(settings.CoreEndpoint, token);
            TokenInput = string.Empty;
            StatusMessage = "Connected. The token is held in memory for this session only.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenConfigDirectory()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ConfigDirectory,
                UseShellExecute = true
            });
            StatusMessage = "Opened the Core configuration directory.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private DesktopSettings CreateSettings()
    {
        if (string.IsNullOrWhiteSpace(CliExecutablePath))
        {
            throw new ArgumentException("The CLI executable path is required.");
        }

        if (CliArguments.Length > 2048 || CliArguments.Contains('\0'))
        {
            throw new ArgumentException("CLI arguments must be 2048 characters or fewer and cannot contain NUL.");
        }

        return new DesktopSettings
        {
            CoreEndpoint = CoreEndpoint.Normalize(Endpoint),
            CliExecutablePath = Path.GetFullPath(CliExecutablePath.Trim()),
            CliArguments = CliArguments.Trim(),
            StartCoreOnLaunch = StartCoreOnLaunch
        };
    }

    private void ApplySavedSettings(DesktopSettings settings)
    {
        Endpoint = settings.CoreEndpoint;
        CliExecutablePath = settings.CliExecutablePath;
        CliArguments = settings.CliArguments;
        StartCoreOnLaunch = settings.StartCoreOnLaunch;
        _savedEndpoint = Endpoint;
        _savedCliExecutablePath = CliExecutablePath;
        _savedCliArguments = CliArguments;
        _savedStartCoreOnLaunch = StartCoreOnLaunch;
        HasLoaded = true;
        OnPropertyChanged(nameof(IsDirty));
    }
}
