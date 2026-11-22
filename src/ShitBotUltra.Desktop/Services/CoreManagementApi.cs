using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace shitbotteam.ultra.desktop.Services;

public sealed partial class CoreApiClient
{
    // Management operations use a bounded response and cover both headers and body
    // with the timeout. expectedSession prevents applying an old UI draft to a
    // newly selected Core, even if the connection changes between UI operations.
    public async Task<JsonElement?> ManagementRequestAsync(
        HttpMethod method, string path, long expectedSession, object? body = null,
        string? revision = null, string? uploadPath = null, CancellationToken cancellationToken = default)
    {
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal) || path.Contains('\\'))
            throw new ArgumentException("Invalid management API path.", nameof(path));
        if (revision is not null && (revision.Length == 0 || revision.Any(c =>
            !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')))
            throw new ArgumentException("Invalid Core revision.", nameof(revision));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(150));
        HttpRequestMessage request;
        lock (_connectionLock)
        {
            if (_connectionVersion != expectedSession)
                throw new InvalidOperationException("The Core connection changed. Reload before continuing.");
            request = CreateRequest(method, path, body, revision is null ? null : $"\"{revision}\"");
        }

        using (request)
        {
            if (uploadPath is not null)
            {
                if (body is not null) throw new ArgumentException("An upload cannot also contain JSON.");
                var stream = new FileStream(uploadPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (stream.Length > 64L * 1024 * 1024)
                {
                    stream.Dispose();
                    throw new InvalidDataException("The selected file exceeds Core's 64 MiB upload limit.");
                }
                request.Content = new StreamContent(stream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            await response.Content.LoadIntoBufferAsync(64L * 1024 * 1024, timeout.Token);
            await EnsureSuccessAsync(response, timeout.Token);
            if (ConnectionVersion != expectedSession)
                throw new InvalidOperationException("The connection changed while the request was in progress. Refresh the previous Core to confirm its state.");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, timeout.Token);
        }
    }

    public static string ManagementSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".."
            || value.Contains('/') || value.Contains('\\') || value.Any(char.IsControl))
            throw new ArgumentException("Invalid management resource identifier.");
        return Uri.EscapeDataString(value);
    }
}
