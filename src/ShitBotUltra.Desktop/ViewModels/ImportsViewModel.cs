using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class ImportSourceRow : ObservableObject
{
    public string Id { get; init; } = "";
    public string Description { get; init; } = "";
    public string IncludeLabel => LocalizationService.Current.Text("Include");
    public string TargetNetworkLabel => LocalizationService.Current.Text("Target network ID");
    internal void RefreshLanguage() { OnPropertyChanged(nameof(IncludeLabel)); OnPropertyChanged(nameof(TargetNetworkLabel)); }
    [ObservableProperty] private bool selected;
    [ObservableProperty] private string networkId = "";
}
public partial class ImportReportRow : ObservableObject
{
    public string Id { get; init; } = "";
    public string Player { get; init; } = "";
    public string ExcludeLabel => LocalizationService.Current.Text("Exclude");
    internal void RefreshLanguage() => OnPropertyChanged(nameof(ExcludeLabel));
    public string State { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Details { get; init; } = "";
    [ObservableProperty] private bool excluded;
}
internal sealed record ImportRowData(string Id, string Player, string State, string Reason, string Details);
internal sealed record ImportNetwork(string Id, string IdentityMode);
internal sealed record ImportSourceRequest(string id, string network_id, string identity_mode);
internal sealed record ImportPreviewRequest(ImportSourceRequest[] sources, string[] exclude);

public partial class ImportsViewModel : ObservableObject
{
    private readonly CoreApiClient _api;
    private long _sourceSession = -1;
    private long _reportSession = -1;
    private readonly List<ImportNetwork> _networks = [];
    private readonly List<ImportRowData> _allRows = [];
    private readonly HashSet<string> _excluded = new(StringComparer.Ordinal);
    private string _previewFingerprint = "";
    private string _reportId = "";
    private bool _ready;
    private bool _committed;
    private bool _restored;
    private int _page;
    private const int PageSize = 100;

    [ObservableProperty] private string selectedSourceId = "";
    [ObservableProperty] private string reportIdInput = "";
    [ObservableProperty] private string networksText = "";
    [ObservableProperty] private string reportSummary = "No report loaded.";
    [ObservableProperty] private string pageText = "";
    [ObservableProperty] private string statusMessage = "Connect to Core and load its staged sources and networks.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    public bool CanEdit => !IsBusy;
    public ObservableCollection<ImportSourceRow> Sources { get; } = [];
    public ObservableCollection<ImportReportRow> Rows { get; } = [];
    public bool HasSelectedSources => Sources.Any(s => s.Selected);
    public bool SourcesBelongToCurrentSession => _sourceSession == _api.ConnectionVersion;
    public ImportsViewModel(CoreApiClient api)
    {
        _api = api;
        LocalizationService.Current.LanguageChanged += () =>
        {
            foreach (var source in Sources) source.RefreshLanguage();
            foreach (var row in Rows) row.RefreshLanguage();
        };
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { StatusMessage = $"{ex.Message} Refresh the Core state before retrying a change."; }
        finally { IsBusy = false; }
    }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var sourceResult = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/imports/sources", session)
            ?? throw new InvalidDataException("Core returned no source list.");
        var networkResult = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/networks", session)
            ?? throw new InvalidDataException("Core returned no networks.");
        var old = _sourceSession == session ? Sources.ToDictionary(s => s.Id) : new Dictionary<string, ImportSourceRow>();
        _networks.Clear();
        foreach (var item in networkResult.GetProperty("items").EnumerateArray())
            _networks.Add(new ImportNetwork(item.GetProperty("id").GetString() ?? "", item.GetProperty("identity_mode").GetString() ?? ""));
        NetworksText = _networks.Count == 0 ? "No configured Core networks. Configure a node first." :
            "Available networks: " + string.Join(", ", _networks.Select(n => n.Id + " (" + n.IdentityMode + ")"));
        Sources.Clear();
        foreach (var item in sourceResult.GetProperty("items").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? "";
            old.TryGetValue(id, out var existing);
            Sources.Add(new ImportSourceRow { Id = id, Description = id + " · " + item.GetProperty("size").GetInt64() + " bytes",
                Selected = existing?.Selected ?? false, NetworkId = existing?.NetworkId ?? _networks.FirstOrDefault()?.Id ?? "" });
        }
        _sourceSession = session;
        StatusMessage = "Sources and networks loaded. Select up to eight source copies and assign each to a network.";
    });

    public Task UploadAsync(string file) => RunAsync(async () =>
    {
        RequireSources();
        var value = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/imports/sources", _sourceSession, uploadPath: file)
            ?? throw new InvalidDataException("Core returned no uploaded source.");
        var id = value.GetProperty("id").GetString() ?? "";
        Sources.Add(new ImportSourceRow { Id = id, Description = System.IO.Path.GetFileName(file) + " · " + id,
            Selected = true, NetworkId = _networks.FirstOrDefault()?.Id ?? "" });
        StatusMessage = "Source copy uploaded to Core. The original file is unchanged.";
    });

    public Task DeleteSourceAsync() => RunAsync(async () =>
    {
        RequireSources();
        if (string.IsNullOrWhiteSpace(SelectedSourceId)) throw new InvalidOperationException("Select a source row first.");
        await _api.ManagementRequestAsync(HttpMethod.Delete, "api/v1/imports/sources/" + CoreApiClient.ManagementSegment(SelectedSourceId), _sourceSession);
        var row = Sources.FirstOrDefault(s => s.Id == SelectedSourceId);
        if (row is not null) Sources.Remove(row);
        _excluded.RemoveWhere(id => id.StartsWith(SelectedSourceId + ":", StringComparison.Ordinal));
        StatusMessage = "Core's staged copy was removed; the original file was not touched.";
    });

    public Task PreviewAsync() => RunAsync(async () =>
    {
        RequireSources();
        var request = BuildRequest();
        var fingerprint = JsonSerializer.Serialize(request);
        var result = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/imports/previews", _sourceSession, request)
            ?? throw new InvalidDataException("Core returned no preview.");
        _reportSession = _sourceSession;
        _previewFingerprint = fingerprint;
        ApplyReport(result);
        StatusMessage = "Review the report. Change exclusions and preview again before committing.";
    });

    public Task ReadReportAsync() => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var value = await _api.ManagementRequestAsync(HttpMethod.Get,
            "api/v1/imports/" + CoreApiClient.ManagementSegment(ReportIdInput), session)
            ?? throw new InvalidDataException("Core returned no report.");
        _reportSession = session;
        _previewFingerprint = "";
        _excluded.Clear();
        foreach (var row in value.GetProperty("rows").EnumerateArray())
            if (row.GetProperty("status").GetString() == "excluded")
                _excluded.Add(row.GetProperty("id").GetString() ?? "");
        ApplyReport(value);
        StatusMessage = "Report loaded. Generate a fresh preview to commit an uncommitted report.";
    });

    public Task CommitAsync() => RunAsync(async () =>
    {
        RequireSources(); RequireReport();
        if (!_ready || _committed || _previewFingerprint.Length == 0 || _previewFingerprint != JsonSerializer.Serialize(BuildRequest()))
            throw new InvalidOperationException("The preview is not ready or its sources, networks or exclusions changed. Generate a fresh preview.");
        var value = await _api.ManagementRequestAsync(HttpMethod.Post,
            "api/v1/imports/" + CoreApiClient.ManagementSegment(_reportId) + "/commit", _reportSession)
            ?? throw new InvalidDataException("Core returned no import report.");
        ApplyReport(value);
        StatusMessage = "Core committed the import. Review the report and its backup information.";
    });

    public Task RestoreAsync() => RunAsync(async () =>
    {
        RequireReport();
        if (!_committed || _restored) throw new InvalidOperationException("Load a committed, unrestored report first.");
        var value = await _api.ManagementRequestAsync(HttpMethod.Post,
            "api/v1/imports/" + CoreApiClient.ManagementSegment(_reportId) + "/restore", _reportSession)
            ?? throw new InvalidDataException("Core returned no import report.");
        ApplyReport(value);
        StatusMessage = "Core completed the restore request. Review the report.";
    });

    public void ClearExclusions() { _excluded.Clear(); ShowPage(); StatusMessage = "Exclusions cleared. Generate a new preview."; }
    public void PreviousPage() { if (_page > 0) { _page--; ShowPage(); } }
    public void NextPage() { if ((_page + 1) * PageSize < _allRows.Count) { _page++; ShowPage(); } }

    private ImportPreviewRequest BuildRequest()
    {
        var selected = Sources.Where(s => s.Selected).ToArray();
        if (selected.Length is < 1 or > 8) throw new InvalidOperationException("Select between one and eight source files.");
        var sources = selected.Select(s =>
        {
            var network = _networks.FirstOrDefault(n => n.Id == s.NetworkId.Trim())
                ?? throw new InvalidOperationException("Every selected source needs an existing target network ID.");
            return new ImportSourceRequest(s.Id, network.Id, network.IdentityMode);
        }).OrderBy(s => s.id, StringComparer.Ordinal).ToArray();
        return new ImportPreviewRequest(sources, _excluded.OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
    private void ApplyReport(JsonElement value)
    {
        _reportId = value.GetProperty("id").GetString() ?? "";
        ReportIdInput = _reportId;
        _ready = value.GetProperty("ready").GetBoolean();
        _committed = value.TryGetProperty("committed_at", out var committed) && committed.ValueKind == JsonValueKind.String;
        _restored = value.TryGetProperty("restored_at", out var restored) && restored.ValueKind == JsonValueKind.String;
        ReportSummary = $"ID: {_reportId} · added: {value.GetProperty("added")} · duplicates: {value.GetProperty("duplicates")} · conflicts: {value.GetProperty("conflicts")} · excluded: {value.GetProperty("excluded")} · ready: {_ready} · committed: {_committed} · restored: {_restored}\n" +
            "Expires: " + (value.TryGetProperty("expires_at", out var expiry) ? expiry.ToString() : "") +
            " · Backup: " + (value.TryGetProperty("backup", out var backup) ? backup.ToString() : "") +
            "\nErrors: " + (value.TryGetProperty("errors", out var errors) ? errors.ToString() : "[]");
        _allRows.Clear();
        foreach (var row in value.GetProperty("rows").EnumerateArray())
        {
            var record = row.GetProperty("record");
            var player = record.GetProperty("player").GetProperty("name").GetString() ?? "";
            var details = "Network: " + record.GetProperty("network_id").GetString() + " · Source: " + row.GetProperty("sources").ToString() +
                " · Owner: " + (record.TryGetProperty("owner", out var owner) ? owner.ToString() : "unbound");
            _allRows.Add(new ImportRowData(row.GetProperty("id").GetString() ?? "", player,
                row.GetProperty("status").GetString() ?? "", row.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "", details));
        }
        _page = 0; ShowPage();
    }
    private void ShowPage()
    {
        foreach (var row in Rows) row.PropertyChanged -= OnRowChanged;
        Rows.Clear();
        foreach (var item in _allRows.Skip(_page * PageSize).Take(PageSize))
        {
            var row = new ImportReportRow { Id = item.Id, Player = item.Player, State = item.State,
                Reason = item.Reason, Details = item.Details, Excluded = _excluded.Contains(item.Id) };
            row.PropertyChanged += OnRowChanged; Rows.Add(row);
        }
        PageText = $"Page {_page + 1} · {_allRows.Count} rows (100 per page)";
    }
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImportReportRow.Excluded) || sender is not ImportReportRow row) return;
        if (row.Excluded) _excluded.Add(row.Id); else _excluded.Remove(row.Id);
    }
    private void RequireSources()
    {
        if (_sourceSession != _api.ConnectionVersion) throw new InvalidOperationException("Load sources for the current Core connection first.");
    }
    private void RequireReport()
    {
        if (_reportSession != _api.ConnectionVersion || string.IsNullOrWhiteSpace(_reportId))
            throw new InvalidOperationException("Load or generate a report for the current Core connection first.");
    }
}
