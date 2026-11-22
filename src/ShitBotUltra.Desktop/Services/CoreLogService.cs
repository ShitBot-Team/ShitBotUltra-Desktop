using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace shitbotteam.ultra.desktop.Services;

public sealed record DesktopLogEntry(DateTimeOffset Timestamp, string Level, string Source, string Message)
{
    public string TimestampText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
}

/// <summary>
/// Stores Desktop lifecycle and Core management-event summaries. It deliberately
/// does not persist chat bodies, credentials, or arbitrary API response payloads.
/// </summary>
public sealed class CoreLogService : IAsyncDisposable
{
    private const int MaximumMemoryEntries = 2000;
    private const long MaximumFileBytes = 5 * 1024 * 1024;
    private const int RetainedFiles = 3;
    private static readonly Regex CredentialPattern = new(
        @"(?i)\b(token|authorization|password|secret)\b\s*[:=]\s*([^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex BearerPattern = new(
        @"(?i)\bBearer\s+[A-Za-z0-9._~+/-]+=*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly object _sync = new();
    private readonly Queue<DesktopLogEntry> _history = new();
    private readonly Channel<DesktopLogEntry> _pending = Channel.CreateBounded<DesktopLogEntry>(
        new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly Task _writer;
    private bool _disposed;

    public string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShitBotUltra",
        "logs");

    public string LogFilePath => Path.Combine(LogDirectory, "desktop.log");

    public event Action<DesktopLogEntry>? EntryReceived;

    public CoreLogService()
    {
        _writer = Task.Run(WriteLoopAsync);
    }

    public IReadOnlyList<DesktopLogEntry> Snapshot()
    {
        lock (_sync)
        {
            return _history.ToArray();
        }
    }

    public void Info(string source, string message) => Write("Information", source, message);

    public void Warning(string source, string message) => Write("Warning", source, message);

    public void Error(string source, string message) => Write("Error", source, message);

    public void Write(string level, string source, string message)
    {
        if (_disposed)
        {
            return;
        }

        var safeMessage = Redact(message ?? string.Empty);
        var entry = new DesktopLogEntry(DateTimeOffset.UtcNow, level, source, safeMessage);
        lock (_sync)
        {
            _history.Enqueue(entry);
            while (_history.Count > MaximumMemoryEntries)
            {
                _history.Dequeue();
            }
        }

        _pending.Writer.TryWrite(entry);
        try
        {
            EntryReceived?.Invoke(entry);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Desktop log subscriber failed: {exception.Message}");
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            await foreach (var entry in _pending.Reader.ReadAllAsync())
            {
                try
                {
                    RotateIfRequired();
                    var line = string.Join(
                        "\t",
                        entry.Timestamp.ToString("O"),
                        Escape(entry.Level),
                        Escape(entry.Source),
                        Escape(entry.Message));
                    await File.AppendAllTextAsync(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException exception)
                {
                    Debug.WriteLine($"Desktop log write failed: {exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    Debug.WriteLine($"Desktop log write failed: {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Desktop log writer stopped: {exception.Message}");
        }
    }

    private void RotateIfRequired()
    {
        if (!File.Exists(LogFilePath) || new FileInfo(LogFilePath).Length < MaximumFileBytes)
        {
            return;
        }

        var oldest = string.Concat(LogFilePath, ".", RetainedFiles);
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = RetainedFiles - 1; index >= 1; index--)
        {
            var source = string.Concat(LogFilePath, ".", index);
            var destination = string.Concat(LogFilePath, ".", index + 1);
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        File.Move(LogFilePath, string.Concat(LogFilePath, ".1"), overwrite: true);
    }

    private static string Redact(string value)
    {
        try
        {
            value = BearerPattern.Replace(value, "Bearer [REDACTED]");
            return CredentialPattern.Replace(value, "$1=[REDACTED]");
        }
        catch (RegexMatchTimeoutException)
        {
            return "[log entry omitted: redaction timed out]";
        }
    }

    private static string Escape(string value) => value
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pending.Writer.TryComplete();
        await _writer;
        EntryReceived = null;
    }
}
