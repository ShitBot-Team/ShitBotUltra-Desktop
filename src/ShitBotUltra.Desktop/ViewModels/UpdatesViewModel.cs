using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class UpdatesViewModel : ObservableObject
{
    private readonly CoreApiClient _api;
    private long _session = -1;
    private bool _enabled;
    private bool _canInstall;
    private bool _available;
    private bool _locked;
    private string _version = "";
    private string _sha256 = "";
    [ObservableProperty] private string hostStatus = "Connect to Core and refresh the host update state.";
    [ObservableProperty] private string releaseDetails = "No release information loaded.";
    [ObservableProperty] private string releaseNotes = "";
    [ObservableProperty] private string message = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    public bool CanEdit => !IsBusy;
    public string DesktopVersion => "Desktop assembly version: " + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown");
    public string CandidateVersion => _version;
    public UpdatesViewModel(CoreApiClient api) => _api = api;

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { Message = $"{ex.Message} Refresh the host state before retrying."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var state = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/updates", session)
            ?? throw new InvalidDataException("Core returned no update state.");
        _session = session; Apply(state); Message = "Host update state refreshed.";
    });

    public Task CheckAsync() => RunAsync(async () =>
    {
        RequireSession();
        if (!_enabled || _locked) throw new InvalidOperationException("This host cannot check for updates in its current state.");
        var state = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/updates/check", _session)
            ?? throw new InvalidDataException("Core returned no update state.");
        Apply(state); Message = "Host update check completed.";
    });

    public Task InstallAsync() => RunAsync(async () =>
    {
        RequireSession();
        if (!_enabled || !_canInstall || !_available || _locked || string.IsNullOrWhiteSpace(_version)
            || _sha256.Length != 64 || !_sha256.All(char.IsAsciiHexDigit))
            throw new InvalidOperationException("The host has no installable release with a valid SHA-256. Refresh and check again.");
        var state = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/updates/install", _session,
            new { version = _version, sha256 = _sha256 })
            ?? throw new InvalidDataException("Core returned no update state.");
        Apply(state);
        Message = "Host update request accepted. A handoff does not confirm installation; refresh or reconnect to check the result.";
    });

    private void RequireSession()
    {
        if (_session != _api.ConnectionVersion || !_api.IsAuthenticated)
            throw new InvalidOperationException("Refresh update state for the current Core connection first.");
    }
    private static string Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.ToString() : "";
    private static bool Bool(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
    private void Apply(JsonElement state)
    {
        _enabled = Bool(state, "enabled"); _canInstall = Bool(state, "can_install");
        var phase = Text(state, "phase");
        _locked = phase is "checking" or "downloading" or "handing_off" or "handed_off";
        HostStatus = _enabled
            ? $"Repository: {Text(state, "repository")} · current host: {Text(state, "current_version")} · phase: {phase}\nChecked: {Text(state, "checked_at")} · backup: {Text(state, "backup")}\nLast error: {Text(state, "last_error")}"
            : "This Core host has not registered an updater. Manage its installation through its own host.";
        _available = false; _version = ""; _sha256 = ""; ReleaseNotes = "";
        if (state.TryGetProperty("release", out var release) && release.ValueKind == JsonValueKind.Object)
        {
            _version = Text(release, "version"); _sha256 = Text(release, "sha256"); _available = Bool(release, "available");
            ReleaseDetails = $"Version: {_version} · available: {_available}\nAsset: {Text(release, "asset_name")} · size: {Text(release, "size")} bytes\nSHA-256: {_sha256}\nRelease: {Text(release, "url")}";
            ReleaseNotes = Text(release, "notes");
        }
        else ReleaseDetails = "No candidate release reported by the host.";
        OnPropertyChanged(nameof(CandidateVersion));
    }
}
