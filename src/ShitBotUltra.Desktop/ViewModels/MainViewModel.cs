using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Models;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly CoreApiClient _apiClient;
    private readonly DesktopSettingsStore _settingsStore;
    private readonly CoreProcessManager _processManager;
    private readonly CoreEventStreamService _eventStream;
    private readonly IUiDispatcher _dispatcher;
    private readonly CoreLogService _logService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    [NotifyPropertyChangedFor(nameof(CoreStateText))]
    [NotifyPropertyChangedFor(nameof(InstanceIdText))]
    [NotifyPropertyChangedFor(nameof(ManagementUrlText))]
    private CoreStatus? status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerSummary))]
    [NotifyPropertyChangedFor(nameof(NodeSummary))]
    [NotifyPropertyChangedFor(nameof(AdapterSummary))]
    private DashboardSnapshot? dashboard;

    [ObservableProperty]
    private string connectionState = "Not connected";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string eventStreamState = "Disconnected";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isManagedProcess;

    [ObservableProperty]
    private bool webUiEnabled;

    [ObservableProperty]
    private bool isWebUiBusy;

    [ObservableProperty]
    private string webUiMessage = string.Empty;

    public ObservableCollection<ConnectionRow> ConnectionRows { get; } = [];

    public string VersionText => Status?.Version ?? "-";

    public string CoreStateText => Status?.State ?? "Unknown";

    public string InstanceIdText => Status is null ? "No Core instance connected." : $"Instance: {Status.InstanceId}";

    public string ManagementUrlText => Status?.Url ?? "Management API URL unavailable.";

    public string PlayerSummary => Dashboard is null
        ? "-"
        : $"{Dashboard.Counts.Online} online / {Dashboard.Counts.Players} known";

    public string NodeSummary => Dashboard is null
        ? $"{Status?.Nodes.Count ?? 0} reported"
        : $"{Dashboard.ConnectedNodes} connected / {Dashboard.Nodes} configured";

    public string AdapterSummary => Dashboard is null
        ? $"{Status?.Adapters.Count ?? 0} reported"
        : $"{Dashboard.ConnectedAdapters} connected / {Dashboard.Adapters} configured";

    public MainViewModel(
        CoreApiClient apiClient,
        DesktopSettingsStore settingsStore,
        CoreProcessManager processManager,
        CoreEventStreamService eventStream,
        IUiDispatcher dispatcher,
        CoreLogService logService)
    {
        _apiClient = apiClient;
        _settingsStore = settingsStore;
        _processManager = processManager;
        _eventStream = eventStream;
        _dispatcher = dispatcher;
        _logService = logService;

        _eventStream.EventReceived += OnEventReceived;
        _eventStream.StateChanged += OnEventStreamStateChanged;
        _processManager.ManagedProcessExited += OnManagedProcessExited;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            _logService.Info("Desktop", "Desktop session initialized.");
            IsManagedProcess = _processManager.IsManagedProcessRunning;
            var endpoint = CoreEndpoint.Parse(settings.CoreEndpoint);
            var reachable = await _apiClient.ProbeAsync(endpoint, cancellationToken);
            ConnectionState = reachable
                ? "Core is reachable. Connect with an admin token in Settings."
                : "Core is not running at the configured address.";

            if (settings.StartCoreOnLaunch)
            {
                await StartCoreAsync();
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            ConnectionState = "Desktop settings could not be loaded.";
        }
    }

    public async Task ConnectAsync(
        string endpointText,
        string adminToken,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("A Core operation is already in progress.");
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _eventStream.StopAsync();
            var endpoint = CoreEndpoint.Parse(endpointText);
            IsManagedProcess = _processManager.IsManagedProcessFor(endpoint);
            _apiClient.Configure(endpoint, adminToken);
            var coreStatus = await _apiClient.GetStatusAsync(cancellationToken);
            ValidateCoreContract(coreStatus);

            Status = coreStatus;
            Dashboard = await _apiClient.GetDashboardAsync(cancellationToken);
            var connections = await _apiClient.GetConnectionsAsync(cancellationToken);
            ReplaceConnections(connections);
            WebUiEnabled = coreStatus.WebUiEnabled;
            await _eventStream.StartAsync();
            ConnectionState = $"Connected to Core {coreStatus.Version}";
            _logService.Info("Core", $"Connected to Core {coreStatus.Version} at {endpoint}.");
        }
        catch (Exception exception)
        {
            _logService.Warning("Core", $"Connection failed ({exception.GetType().Name}).");
            _apiClient.ClearCredentials();
            Status = null;
            Dashboard = null;
            WebUiEnabled = false;
            ConnectionRows.Clear();
            ConnectionState = "Connection failed";
            ErrorMessage = exception.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_apiClient.IsAuthenticated || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await RefreshCoreAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            ConnectionState = "Refresh failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartCoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        Uri? requestedEndpoint = null;
        try
        {
            var settings = await _settingsStore.LoadAsync();
            var endpoint = CoreEndpoint.Parse(settings.CoreEndpoint);
            requestedEndpoint = endpoint;
            var disposition = await _processManager.StartOrAttachAsync(
                settings.CliExecutablePath,
                settings.CliArguments,
                endpoint);
            IsManagedProcess = disposition == CoreStartDisposition.StartedByDesktop;
            _logService.Info("Process", disposition == CoreStartDisposition.StartedByDesktop
                ? "Started the local Core host process."
                : "Attached to an existing Core endpoint.");
            ConnectionState = disposition == CoreStartDisposition.StartedByDesktop
                ? "Core started by Desktop. Sign in to load management data."
                : "Attached to an existing Core instance.";

            if (_apiClient.IsAuthenticated && Equals(_apiClient.ConfiguredEndpoint, endpoint))
            {
                try
                {
                    await RefreshCoreAsync();
                }
                catch (Exception exception)
                {
                    ErrorMessage = exception.Message;
                    ConnectionState = "Core is running, but its management data could not be refreshed.";
                }
            }
            else
            {
                await _eventStream.StopAsync();
                _apiClient.ClearCredentials();
                Status = null;
                Dashboard = null;
                WebUiEnabled = false;
                ConnectionRows.Clear();
                ConnectionState = disposition == CoreStartDisposition.StartedByDesktop
                    ? "Core started by Desktop. Sign in to load management data."
                    : "Core endpoint reachable. Sign in to load management data.";
            }
        }
        catch (Exception exception)
        {
            IsManagedProcess = _processManager.IsManagedProcessFor(
                _apiClient.IsAuthenticated ? _apiClient.ConfiguredEndpoint : requestedEndpoint);
            ErrorMessage = exception.Message;
            _logService.Error("Process", $"Core start/attach failed: {exception.Message}");
            ConnectionState = "Core could not be started or reached.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopCoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (!IsManagedProcess)
            {
                ErrorMessage = "This Core instance was not started by this Desktop session. It will not be stopped here.";
                return;
            }

            var stopped = await _processManager.StopManagedAsync();
            if (!stopped)
            {
                ErrorMessage = "Core did not exit after the graceful stop request. It is still running; use Force Stop only if necessary.";
                return;
            }

            IsManagedProcess = false;
            Status = null;
            Dashboard = null;
            WebUiEnabled = false;
            ConnectionRows.Clear();
            ConnectionState = "Managed Core stopped.";
            _logService.Info("Process", "Gracefully stopped the Core process started by this Desktop session.");
            await _eventStream.StopAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ForceStopCoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("A Core operation is already in progress.");
        }

        if (!IsManagedProcess)
        {
            throw new InvalidOperationException("Only a process started by this Desktop session can be force-stopped.");
        }

        IsBusy = true;
        try
        {
            await _processManager.ForceStopManagedAsync(cancellationToken);
            IsManagedProcess = false;
            Status = null;
            Dashboard = null;
            WebUiEnabled = false;
            ConnectionRows.Clear();
            ConnectionState = "Managed Core was force-stopped.";
            _logService.Warning("Process", "Force-stopped the Core process tree after explicit user confirmation.");
            await _eventStream.StopAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }



    [RelayCommand]
    private async Task ToggleWebUiAsync()
    {
        if (!_apiClient.IsAuthenticated || IsWebUiBusy)
        {
            return;
        }

        IsWebUiBusy = true;
        WebUiMessage = string.Empty;
        try
        {
            var requestedState = !WebUiEnabled;
            await _apiClient.SetWebUiEnabledAsync(requestedState);
            WebUiEnabled = requestedState;
            WebUiMessage = requestedState
                ? "WebUI is enabled for this Core session."
                : "WebUI is disabled for this Core session. The management API remains available.";
        }
        catch (Exception exception)
        {
            WebUiMessage = exception.Message;
        }
        finally
        {
            IsWebUiBusy = false;
        }
    }

    [RelayCommand]
    private void OpenWebUi()
    {
        if (!WebUiEnabled)
        {
            WebUiMessage = "Enable WebUI before opening it.";
            return;
        }

        var endpoint = _apiClient.ConfiguredEndpoint;
        if (endpoint is null)
        {
            WebUiMessage = "Connect to Core before opening WebUI.";
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = endpoint.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            WebUiMessage = exception.Message;
        }
    }

    private async Task RefreshCoreAsync()
    {
        var coreStatus = await _apiClient.GetStatusAsync();
        ValidateCoreContract(coreStatus);
        Status = coreStatus;
        Dashboard = await _apiClient.GetDashboardAsync();
        ReplaceConnections(await _apiClient.GetConnectionsAsync());
        WebUiEnabled = coreStatus.WebUiEnabled;
        ConnectionState = $"Connected to Core {coreStatus.Version}";
    }

    private void ReplaceConnections(CoreConnectionsSnapshot snapshot)
    {
        ConnectionRows.Clear();
        foreach (var node in snapshot.Nodes)
        {
            ConnectionRows.Add(ConnectionRow.FromNode(node));
        }

        foreach (var adapter in snapshot.Adapters)
        {
            ConnectionRows.Add(ConnectionRow.FromAdapter(adapter));
        }
    }

    private static void ValidateCoreContract(CoreStatus value)
    {
        if (!string.Equals(value.Component, "shitbotteam.ultra.core", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The endpoint did not identify itself as ShitBotUltra Core.");
        }

        if (value.AdminApiVersion != 1)
        {
            throw new InvalidDataException($"Unsupported Core Admin API version: {value.AdminApiVersion}.");
        }

        if (value.BridgeProtocolVersion != 1)
        {
            throw new InvalidDataException($"Unsupported Core Bridge protocol version: {value.BridgeProtocolVersion}.");
        }
    }

    private void OnEventReceived(CoreEvent value)
    {
        _logService.Info("Core event", $"{value.Type} (event id: {value.Id ?? "unavailable"}).");
        _dispatcher.Enqueue(() =>
        {
            if (value.Type is "reset" or "webui.changed")
            {
                _ = RefreshAsync();
            }
        });
    }

    private void OnEventStreamStateChanged(string value)
    {
        _logService.Info("Event stream", value);
        _dispatcher.Enqueue(() => EventStreamState = value);
    }

    private void OnManagedProcessExited(int exitCode)
    {
        _dispatcher.Enqueue(() =>
        {
            // An exit notification can arrive after a replacement process starts,
            // or while Desktop is connected to a different independent Core.
            if (_processManager.IsManagedProcessRunning) return;
            var wasCurrent = IsManagedProcess;
            IsManagedProcess = false;
            _logService.Warning("Process", $"Managed Core process exited with code {exitCode}.");
            if (!wasCurrent) return;
            Status = null;
            Dashboard = null;
            WebUiEnabled = false;
            ConnectionRows.Clear();
            ConnectionState = $"Managed Core exited (code {exitCode}).";
            _ = _eventStream.StopAsync();
        });
    }

    public async ValueTask DisposeAsync()
    {
        _eventStream.EventReceived -= OnEventReceived;
        _eventStream.StateChanged -= OnEventStreamStateChanged;
        _processManager.ManagedProcessExited -= OnManagedProcessExited;
        await _eventStream.DisposeAsync();
        _processManager.Dispose();
    }
}
