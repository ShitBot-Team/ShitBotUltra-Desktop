using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class ConfigurationViewModel : ObservableObject
{
    private readonly CoreApiClient _apiClient;
    private string _savedText = string.Empty;

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private string revision = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Connect to Core to edit its configuration.";

    [ObservableProperty]
    private bool restartRequired;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    public bool IsDirty => HasLoaded && !string.Equals(Text, _savedText, StringComparison.Ordinal);

    public ConfigurationViewModel(CoreApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnHasLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDirty));
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (HasLoaded && IsDirty)
        {
            StatusMessage = "An unsaved draft is open. Save it or explicitly discard it before reloading.";
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

        if (!_apiClient.IsAuthenticated)
        {
            StatusMessage = "Connect to Core in Settings before loading configuration.";
            return;
        }

        IsBusy = true;
        try
        {
            var snapshot = await _apiClient.GetConfigurationAsync(cancellationToken);
            Text = snapshot.Document.Text;
            _savedText = Text;
            Revision = snapshot.ETag;
            RestartRequired = false;
            HasLoaded = true;
            OnPropertyChanged(nameof(IsDirty));
            StatusMessage = "Configuration loaded. Saving uses the Core revision check.";
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
    private Task LoadConfigurationAsync() => LoadAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!_apiClient.IsAuthenticated || IsBusy)
        {
            StatusMessage = "Connect to Core before saving.";
            return;
        }

        if (!HasLoaded || string.IsNullOrWhiteSpace(Revision))
        {
            StatusMessage = "Load the current configuration before saving.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiClient.SaveConfigurationAsync(Text, Revision);
            Text = result.Document.Text;
            _savedText = Text;
            Revision = $"\"{result.Document.Revision}\"";
            RestartRequired = result.RestartRequired;
            OnPropertyChanged(nameof(IsDirty));
            StatusMessage = result.RestartRequired
                ? "Configuration saved. Restart the Core host for changes to become active."
                : "Configuration saved.";
        }
        catch (CoreApiException exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.Conflict
            || exception.StatusCode == System.Net.HttpStatusCode.PreconditionRequired)
        {
            StatusMessage = $"Configuration conflict ({exception.Code}). Your draft was kept. Reload before deciding how to merge it.";
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
}
