using System.Diagnostics;

namespace shitbotteam.ultra.desktop.Services;

public enum CoreStartDisposition
{
    StartedByDesktop,
    AttachedToExisting
}

public sealed class CoreProcessManager : IDisposable
{
    private readonly CoreApiClient _apiClient;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private Process? _managedProcess;
    private Uri? _managedEndpoint;
    private bool _disposed;

    public CoreProcessManager(CoreApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public event Action<int>? ManagedProcessExited;

    public bool IsManagedProcessRunning => _managedProcess is { HasExited: false };

    public bool IsManagedProcessFor(Uri? endpoint) => endpoint is not null
        && Equals(_managedEndpoint, endpoint) && IsManagedProcessRunning;

    public async Task<CoreStartDisposition> StartOrAttachAsync(
        string executablePath,
        string arguments,
        Uri endpoint,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_managedProcess is { HasExited: false })
            {
                if (!Equals(_managedEndpoint, endpoint))
                {
                    throw new InvalidOperationException("A Core process is already managed at another endpoint. Stop or reconnect to that process before starting another.");
                }

                if (!await _apiClient.ProbeAsync(endpoint, cancellationToken))
                {
                    throw new InvalidOperationException("The managed Core process is still running, but the configured endpoint does not reach it.");
                }

                return CoreStartDisposition.StartedByDesktop;
            }

            ReleaseExitedProcess();
            if (await _apiClient.ProbeAsync(endpoint, cancellationToken))
            {
                return CoreStartDisposition.AttachedToExisting;
            }

            if (!endpoint.IsLoopback || endpoint.Scheme != Uri.UriSchemeHttp)
            {
                throw new InvalidOperationException("Desktop can only start a local HTTP Core host. Remote or local HTTPS instances must be started by their own host.");
            }

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("A CLI executable path is required.", nameof(executablePath));
            }

            arguments ??= string.Empty;
            if (arguments.Length > 2048 || arguments.Contains('\0'))
            {
                throw new ArgumentException("CLI arguments must be 2048 characters or fewer and cannot contain NUL.", nameof(arguments));
            }

            var fullPath = Path.GetFullPath(executablePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The bundled sbu.exe was not found. Set its path in Desktop settings.", fullPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(fullPath)!,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows did not create the Core host process.");
            process.EnableRaisingEvents = true;
            process.Exited += OnManagedProcessExited;
            _managedProcess = process;
            _managedEndpoint = endpoint;
            WindowsConsoleControl.TryHideConsole((uint)process.Id);

            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    var exitCode = process.ExitCode;
                    ReleaseExitedProcess();
                    throw new InvalidOperationException($"sbu.exe exited before Core became ready (exit code {exitCode}).");
                }

                if (await _apiClient.ProbeAsync(endpoint, cancellationToken))
                {
                    return CoreStartDisposition.StartedByDesktop;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            }

            await RequestGracefulStopCoreAsync(process, TimeSpan.FromSeconds(8), cancellationToken);
            throw new TimeoutException("Core did not expose its management API within 30 seconds.");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<bool> StopManagedAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_managedProcess is not { HasExited: false } process)
            {
                ReleaseExitedProcess();
                return true;
            }

            var sent = WindowsConsoleControl.TrySendInterrupt((uint)process.Id);
            if (!sent)
            {
                return false;
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken)
                    .WaitAsync(timeout ?? TimeSpan.FromSeconds(15), cancellationToken);
            }
            catch (TimeoutException)
            {
                return false;
            }

            ReleaseExitedProcess();
            return true;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task ForceStopManagedAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_managedProcess is not { HasExited: false } process)
            {
                ReleaseExitedProcess();
                return;
            }

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
            ReleaseExitedProcess();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task RequestGracefulStopCoreAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            ReleaseExitedProcess();
            return;
        }

        if (WindowsConsoleControl.TrySendInterrupt((uint)process.Id))
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Leave the process running rather than force-killing a host after a startup timeout.
            }
        }
    }

    private void OnManagedProcessExited(object? sender, EventArgs args)
    {
        var process = sender as Process;
        var exitCode = -1;
        try
        {
            if (process is not null)
            {
                exitCode = process.ExitCode;
            }
        }
        catch (InvalidOperationException)
        {
            // The process handle may already have been disposed during application shutdown.
        }

        ManagedProcessExited?.Invoke(exitCode);
    }

    private void ReleaseExitedProcess()
    {
        if (_managedProcess is not { HasExited: true } process)
        {
            return;
        }

        process.Exited -= OnManagedProcessExited;
        process.Dispose();
        _managedProcess = null;
        _managedEndpoint = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_managedProcess is { } process)
        {
            process.Exited -= OnManagedProcessExited;
            process.Dispose();
            _managedProcess = null;
            _managedEndpoint = null;
        }

        _lifecycleLock.Dispose();
    }
}
