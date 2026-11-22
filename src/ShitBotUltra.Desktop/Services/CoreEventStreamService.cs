using shitbotteam.ultra.desktop.Models;

namespace shitbotteam.ultra.desktop.Services;

public sealed class CoreEventStreamService : IAsyncDisposable
{
    private readonly CoreApiClient _apiClient;
    private const int MaximumBufferedEvents = 500;
    private readonly object _sync = new();
    private readonly Queue<CoreEvent> _history = new();
    private CancellationTokenSource? _cancellation;
    private Task? _runner;

    public CoreEventStreamService(CoreApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public event Action<CoreEvent>? EventReceived;
    public event Action<string>? StateChanged;
    public event Action? HistoryCleared;

    public IReadOnlyList<CoreEvent> Snapshot()
    {
        lock (_sync)
        {
            return _history.ToArray();
        }
    }

    public async Task StartAsync()
    {
        await StopAsync();
        var cancellation = new CancellationTokenSource();
        StateChanged?.Invoke("Connecting");
        lock (_sync)
        {
            _history.Clear();
            _cancellation = cancellation;
            _runner = RunAsync(cancellation.Token);
        }

        HistoryCleared?.Invoke();
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task? runner;
        lock (_sync)
        {
            cancellation = _cancellation;
            runner = _runner;
            _cancellation = null;
            _runner = null;
        }

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        if (runner is not null)
        {
            try
            {
                await runner;
            }
            catch (OperationCanceledException)
            {
                // Expected when a session is disconnected.
            }
        }

        cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        string? lastEventId = null;
        var retryDelay = TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var value in _apiClient.ReadEventsAsync(
                    lastEventId,
                    () => StateChanged?.Invoke("Connected"),
                    cancellationToken))
                {
                    if (!string.IsNullOrWhiteSpace(value.Id))
                    {
                        lastEventId = value.Id;
                    }

                    var displayValue = value with
                    {
                        Data = value.Data.Length > 4096 ? string.Concat(value.Data[..4093], "...") : value.Data
                    };
                    lock (_sync)
                    {
                        _history.Enqueue(displayValue);
                        while (_history.Count > MaximumBufferedEvents)
                        {
                            _history.Dequeue();
                        }
                    }

                    EventReceived?.Invoke(displayValue);
                    retryDelay = TimeSpan.FromSeconds(1);
                }

                throw new IOException("The Core event stream closed unexpectedly.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (CoreApiException exception) when (
                exception.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                StateChanged?.Invoke("Authentication expired");
                break;
            }
            catch (Exception)
            {
                StateChanged?.Invoke("Reconnecting");
                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 30));
            }
        }

        StateChanged?.Invoke("Disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
