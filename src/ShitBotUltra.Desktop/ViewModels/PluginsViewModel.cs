using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public sealed record PluginRow(string Id, string State);

public partial class PluginsViewModel : ObservableObject
{
    private readonly CoreApiClient _api;
    private long _session = -1;
    private string _revision = string.Empty;
    private string _savedDraft = string.Empty;
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    [ObservableProperty] private string selectedId = string.Empty;
    [ObservableProperty] private string currentId = string.Empty;
    [ObservableProperty] private string versionRevision = string.Empty;
    [ObservableProperty] private string metadata = string.Empty;
    [ObservableProperty] private string configuration = "{}";
    [ObservableProperty] private string grant = "{\"capabilities\":[],\"networks\":[],\"routes\":[],\"nodes\":[]}";
    [ObservableProperty] private string providerName = string.Empty;
    [ObservableProperty] private string providerRequest = "{}";
    [ObservableProperty] private string providerResult = string.Empty;
    [ObservableProperty] private string statusMessage = "Connect to Core and refresh the plugin list.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;

    public ObservableCollection<PluginRow> Plugins { get; } = [];
    public ObservableCollection<string> Versions { get; } = [];
    public bool CanEdit => !IsBusy;
    public bool IsDirty => !string.IsNullOrEmpty(CurrentId) && Draft() != _savedDraft;
    private string Draft() => JsonSerializer.Serialize(new[] { VersionRevision, Configuration, Grant });

    public PluginsViewModel(CoreApiClient api) => _api = api;

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { StatusMessage = $"{ex.Message} Refresh the Core state before retrying a change."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        await RefreshListAsync(session);
        StatusMessage = "Plugin list refreshed.";
    });

    private async Task RefreshListAsync(long session)
    {
        var value = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/plugins", session)
            ?? throw new InvalidDataException("Core returned no plugin list.");
        Plugins.Clear();
        foreach (var item in value.GetProperty("items").EnumerateArray())
            Plugins.Add(new PluginRow(item.GetProperty("id").GetString() ?? "", item.TryGetProperty("state", out var state) ? state.GetString() ?? "" : ""));
    }

    public Task OpenAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(SelectedId)) throw new InvalidOperationException("Select a plugin first.");
        var session = _api.ConnectionVersion;
        var value = await _api.ManagementRequestAsync(HttpMethod.Get, Path(SelectedId), session)
            ?? throw new InvalidDataException("Core returned no plugin details.");
        _session = session;
        Apply(value);
        StatusMessage = "Plugin loaded. Review its declared capabilities before granting permissions.";
    });

    public Task ImportAsync(string file) => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var value = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/plugins", session, uploadPath: file)
            ?? throw new InvalidDataException("Core returned no plugin details.");
        await RefreshListAsync(session);
        SelectedId = value.GetProperty("id").GetString() ?? "";
        StatusMessage = "Package imported. Select and load it to review permissions; import does not enable it.";
    });

    public Task EnableAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        using var config = JsonDocument.Parse(Configuration);
        using var grants = JsonDocument.Parse(Grant);
        if (config.RootElement.ValueKind != JsonValueKind.Object || grants.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Configuration and grant must be JSON objects.");
        var body = new { version_revision = VersionRevision, grant = grants.RootElement.Clone(), configuration = config.RootElement.Clone() };
        var value = await _api.ManagementRequestAsync(HttpMethod.Post, Path(CurrentId) + "/enable", _session, body, _revision)
            ?? throw new InvalidDataException("Core returned no plugin details.");
        Apply(value);
        await RefreshListAsync(_session);
        StatusMessage = "Plugin enable request completed.";
    });

    public Task DisableAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        var value = await _api.ManagementRequestAsync(HttpMethod.Post, Path(CurrentId) + "/disable", _session, revision: _revision)
            ?? throw new InvalidDataException("Core returned no plugin details.");
        ApplyPreservingDraft(value);
        await RefreshListAsync(_session);
        StatusMessage = "Plugin disabled. Local configuration and grant drafts were preserved.";
    });

    public Task RemoveAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        await _api.ManagementRequestAsync(HttpMethod.Delete, Path(CurrentId), _session, revision: _revision);
        CurrentId = ""; _revision = ""; _savedDraft = ""; Metadata = ""; Versions.Clear();
        await RefreshListAsync(_session);
        StatusMessage = "Plugin package removed. Core retains plugin data according to its policy.";
    });

    public Task ProviderAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        using var request = JsonDocument.Parse(ProviderRequest);
        var value = await _api.ManagementRequestAsync(HttpMethod.Post,
            Path(CurrentId) + "/providers/" + CoreApiClient.ManagementSegment(ProviderName), _session,
            request.RootElement.Clone());
        ProviderResult = value is null ? "" : JsonSerializer.Serialize(value.Value, Pretty);
        StatusMessage = "Provider returned data.";
    });

    private void Apply(JsonElement value)
    {
        CurrentId = value.GetProperty("id").GetString() ?? "";
        _revision = value.GetProperty("revision").GetString() ?? "";
        Versions.Clear();
        foreach (var item in value.GetProperty("versions").EnumerateArray())
            Versions.Add(item.GetProperty("revision").GetString() ?? "");
        var active = value.GetProperty("active_revision").GetString();
        VersionRevision = string.IsNullOrEmpty(active) ? Versions.FirstOrDefault() ?? "" : active;
        Configuration = value.TryGetProperty("configuration", out var config) && config.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Serialize(config, Pretty) : "{}";
        Grant = value.TryGetProperty("grant", out var grantValue) && grantValue.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Serialize(grantValue, Pretty) : "{\"capabilities\":[],\"networks\":[],\"routes\":[],\"nodes\":[]}";
        Metadata = JsonSerializer.Serialize(value, Pretty);
        _savedDraft = Draft();
    }

    private void ApplyPreservingDraft(JsonElement value)
    {
        var version = VersionRevision; var config = Configuration; var grantValue = Grant;
        Apply(value);
        VersionRevision = version; Configuration = config; Grant = grantValue;
    }

    private void RequireCurrent()
    {
        if (string.IsNullOrWhiteSpace(CurrentId) || string.IsNullOrWhiteSpace(_revision))
            throw new InvalidOperationException("Load a plugin first.");
        if (_session != _api.ConnectionVersion)
            throw new InvalidOperationException("The Core connection changed. Keep your draft and load the plugin again before changing it.");
    }

    private static string Path(string id) => "api/v1/plugins/" + CoreApiClient.ManagementSegment(id);
}
