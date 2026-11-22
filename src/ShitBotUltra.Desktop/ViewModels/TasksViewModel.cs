using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public sealed record ScheduledTaskRow(string Id, string Name, bool Enabled, bool Running, string Detail);
public sealed record TaskRunRow(string Id, string Task, string Status, string Detail);

public partial class TasksViewModel : ObservableObject
{
    private readonly CoreApiClient _api;
    private long _session = -1;
    private string _revision = "";
    private bool _paused;
    private Uri? _pendingEndpoint;
    private string _unknownRun = "";
    private string _acknowledgedUnknown = "";
    [ObservableProperty] private string selectedId = "";
    [ObservableProperty] private string pendingId = "";
    [ObservableProperty] private string schedulerStatus = "Connect to Core and refresh the task snapshot.";
    [ObservableProperty] private string runDetails = "";
    [ObservableProperty] private string statusMessage = "Task configuration can be edited in Configuration sections.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    public bool CanEdit => !IsBusy;
    public bool HasUncertainResult => !string.IsNullOrEmpty(PendingId) || !string.IsNullOrEmpty(_unknownRun);
    public ObservableCollection<ScheduledTaskRow> Tasks { get; } = [];
    public ObservableCollection<TaskRunRow> Runs { get; } = [];
    public TasksViewModel(CoreApiClient api) => _api = api;

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
    public Task RefreshAsync() => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var snapshot = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/tasks", session)
            ?? throw new InvalidDataException("Core returned no task snapshot.");
        var history = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/task-runs?limit=100&offset=0", session)
            ?? throw new InvalidDataException("Core returned no task history.");
        _session = session;
        _revision = snapshot.GetProperty("revision").GetString() ?? "";
        _paused = snapshot.GetProperty("paused").GetBoolean();
        SchedulerStatus = (_paused ? "Paused: " + snapshot.GetProperty("reason") : "Scheduler active") + " · snapshot " + _revision;
        Tasks.Clear();
        foreach (var item in snapshot.GetProperty("items").EnumerateArray())
        {
            var config = item.GetProperty("config");
            Tasks.Add(new ScheduledTaskRow(config.GetProperty("id").GetString() ?? "", config.GetProperty("name").GetString() ?? "",
                config.GetProperty("enabled").GetBoolean(), item.GetProperty("running").GetBoolean(),
                "Next: " + (item.TryGetProperty("next_at", out var next) ? next.ToString() : "—") + " · " + config.GetProperty("action").GetProperty("kind")));
        }
        Runs.Clear();
        foreach (var item in history.GetProperty("items").EnumerateArray())
            Runs.Add(new TaskRunRow(item.GetProperty("id").GetString() ?? "", item.GetProperty("task_name").GetString() ?? "",
                item.GetProperty("status").GetString() ?? "", item.GetProperty("created_at") + " · " + item.GetProperty("message")));
        StatusMessage = "Snapshot and latest 100 runs loaded. Saved configuration changes require a Core restart.";
    });
    public Task ExecuteAsync() => RunAsync(async () =>
    {
        RequireSnapshot();
        if (!string.IsNullOrEmpty(PendingId)) throw new InvalidOperationException("A previous request is unresolved. Query its ID or verify its outcome before releasing it.");
        var item = Tasks.FirstOrDefault(t => t.Id == SelectedId) ?? throw new InvalidOperationException("Select a running task first.");
        if (_paused || !item.Enabled || item.Running) throw new InvalidOperationException("The scheduler is paused or this task is disabled or already running.");
        var latest = await _api.ManagementRequestAsync(HttpMethod.Get,
            "api/v1/task-runs?limit=1&offset=0&task_id=" + Uri.EscapeDataString(item.Id), _session)
            ?? throw new InvalidDataException("Core returned no task history.");
        var rows = latest.GetProperty("items");
        if (rows.GetArrayLength() > 0 && rows[0].GetProperty("status").GetString() == "unknown")
        {
            _unknownRun = rows[0].GetProperty("id").GetString() ?? "";
            if (_unknownRun != _acknowledgedUnknown)
                throw new InvalidOperationException("The latest result is unknown. Verify the target and acknowledge it before executing again. Run ID: " + _unknownRun);
        }
        PendingId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _pendingEndpoint = _api.ConfiguredEndpoint;
        try
        {
            var result = await _api.ManagementRequestAsync(HttpMethod.Post,
                "api/v1/tasks/" + CoreApiClient.ManagementSegment(item.Id) + "/run", _session,
                new { request_id = PendingId, revision = _revision }) ?? throw new InvalidDataException("Core returned no task run.");
            ShowRun(result);
            StatusMessage = "Request accepted. Query its ID until the final result is known.";
        }
        catch (CoreApiException ex) when (ex.Code is "INVALID_INPUT" or "FORBIDDEN" or "NOT_FOUND" or "REVISION_CONFLICT" or "BUSY" or "RATE_LIMITED")
        {
            PendingId = ""; _pendingEndpoint = null;
            throw;
        }
    });
    public Task QueryPendingAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(PendingId)) throw new InvalidOperationException("There is no pending request ID.");
        if (!Equals(_pendingEndpoint, _api.ConfiguredEndpoint) || !_api.IsAuthenticated)
            throw new InvalidOperationException("Reconnect to the Core endpoint where this request was sent.");
        var session = _api.ConnectionVersion;
        var value = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/task-runs/" + CoreApiClient.ManagementSegment(PendingId), session)
            ?? throw new InvalidDataException("Core returned no task run.");
        ShowRun(value);
        StatusMessage = "Request state loaded. Unknown results require verification at the target.";
    });
    public void AcknowledgeVerified()
    {
        if (!string.IsNullOrEmpty(_unknownRun)) _acknowledgedUnknown = _unknownRun;
        if (!string.IsNullOrEmpty(PendingId))
        {
            _acknowledgedUnknown = PendingId;
            PendingId = ""; _pendingEndpoint = null;
        }
        StatusMessage = "The user confirmed checking the previous outcome. A new request can be made explicitly.";
    }
    private void ShowRun(JsonElement run)
    {
        RunDetails = JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true });
        var state = run.GetProperty("status").GetString() ?? "";
        if (state == "unknown") _unknownRun = run.GetProperty("id").GetString() ?? "";
        if (state is "succeeded" or "failed" or "skipped" or "canceled") { PendingId = ""; _pendingEndpoint = null; }
    }
    private void RequireSnapshot()
    {
        if (_session != _api.ConnectionVersion || string.IsNullOrWhiteSpace(_revision))
            throw new InvalidOperationException("Refresh the task snapshot for this Core connection first.");
    }
}
