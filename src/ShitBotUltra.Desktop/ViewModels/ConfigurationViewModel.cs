using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class ConfigurationViewModel : ObservableObject
{
    private readonly CoreApiClient _apiClient;
    private string _savedText = string.Empty;
    private long _loadedConnectionVersion = -1;

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
            var requestVersion = _apiClient.ConnectionVersion;
            var previousText = Text;
            var snapshot = await _apiClient.GetConfigurationAsync(cancellationToken);
            if (requestVersion != _apiClient.ConnectionVersion)
            {
                StatusMessage = "The Core connection changed while loading. The draft was kept; load again.";
                return;
            }
            if (!string.Equals(previousText, Text, StringComparison.Ordinal))
            {
                StatusMessage = "The draft changed while loading and was kept. Reload again when ready.";
                return;
            }
            _loadedConnectionVersion = requestVersion;
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
    private async Task ValidateAsync()
    {
        if (!CanUseLoadedSession() || IsBusy) return;
        IsBusy = true;
        var submittedText = Text;
        var requestVersion = _loadedConnectionVersion;
        try
        {
            await _apiClient.ValidateConfigurationAsync(submittedText);
            StatusMessage = requestVersion != _apiClient.ConnectionVersion
                ? "The Core connection changed; validate the draft again after reloading."
                : !string.Equals(submittedText, Text, StringComparison.Ordinal)
                    ? "The submitted version was valid, but the draft has changed since validation."
                    : "Configuration is valid according to Core.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally { IsBusy = false; }
    }

    private bool CanUseLoadedSession()
    {
        if (!_apiClient.IsAuthenticated || !HasLoaded || string.IsNullOrWhiteSpace(Revision))
        {
            StatusMessage = "Connect to Core and load the configuration first.";
            return false;
        }
        if (_loadedConnectionVersion != _apiClient.ConnectionVersion)
        {
            StatusMessage = "The Core connection changed. Copy your draft if needed, then discard and reload before saving.";
            return false;
        }
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || !CanUseLoadedSession()) return;

        IsBusy = true;
        var submittedText = Text;
        var requestVersion = _loadedConnectionVersion;
        try
        {
            var result = await _apiClient.SaveConfigurationAsync(submittedText, Revision);
            if (requestVersion != _apiClient.ConnectionVersion)
            {
                StatusMessage = "The save completed on the previous Core connection. Your current draft was kept; reload before saving again.";
                return;
            }
            var changedDuringSave = !string.Equals(Text, submittedText, StringComparison.Ordinal);
            _savedText = result.Document.Text;
            if (!changedDuringSave) Text = result.Document.Text;
            Revision = $"\"{result.Document.Revision}\"";
            RestartRequired = result.RestartRequired;
            OnPropertyChanged(nameof(IsDirty));
            StatusMessage = changedDuringSave
                ? "The submitted version was saved. Newer edits remain unsaved."
                : result.RestartRequired
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
