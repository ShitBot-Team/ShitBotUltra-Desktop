using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using shitbotteam.ultra.desktop.Models;

namespace shitbotteam.ultra.desktop.Services;

public sealed partial class CoreApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _httpClient;
    private readonly object _connectionLock = new();
    private Uri? _endpoint;
    private string? _adminToken;
    private long _connectionVersion;

    public CoreApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Uri? ConfiguredEndpoint
    {
        get
        {
            lock (_connectionLock)
            {
                return _endpoint;
            }
        }
    }

    // Changes on every connection attempt and disconnect, including reconnects
    // to the same address. Drafts are bound to the session that loaded them.
    public long ConnectionVersion
    {
        get { lock (_connectionLock) { return _connectionVersion; } }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_connectionLock)
            {
                return _endpoint is not null && !string.IsNullOrEmpty(_adminToken);
            }
        }
    }

    public void Configure(Uri endpoint, string adminToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var normalizedEndpoint = CoreEndpoint.Parse(endpoint.AbsoluteUri);
        var normalizedToken = adminToken.Trim();
        var tokenBytes = Encoding.UTF8.GetByteCount(normalizedToken);
        if (tokenBytes is < 32 or > 4096
            || normalizedToken.Contains('\r')
            || normalizedToken.Contains('\n')
            || normalizedToken.Contains('\0'))
        {
            throw new ArgumentException("The admin token must contain 32-4096 UTF-8 bytes and no control separators.", nameof(adminToken));
        }

        lock (_connectionLock)
        {
            _endpoint = normalizedEndpoint;
            _adminToken = normalizedToken;
            _connectionVersion++;
        }
    }

    public void ClearCredentials()
    {
        lock (_connectionLock)
        {
            _adminToken = null;
            _connectionVersion++;
        }
    }

    public async Task<bool> ProbeAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        var normalizedEndpoint = CoreEndpoint.Parse(endpoint.AbsoluteUri);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(normalizedEndpoint, "api/v1/status"));
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            return response.IsSuccessStatusCode
                || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                || (int)response.StatusCode >= 500;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public Task<CoreStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<CoreStatus>(HttpMethod.Get, "api/v1/status", null, null, cancellationToken);
    }

    public async Task<DashboardSnapshot?> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendJsonAsync<DashboardSnapshot>(
                HttpMethod.Get,
                "api/v1/dashboard",
                null,
                null,
                cancellationToken);
        }
        catch (CoreApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Older Core builds expose status and connections before dashboard aggregation was added.
            return null;
        }
    }

    public Task<CoreConnectionsSnapshot> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<CoreConnectionsSnapshot>(
            HttpMethod.Get,
            "api/v1/connections",
            null,
            null,
            cancellationToken);
    }

    public async Task SetWebUiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Put,
            "api/v1/webui",
            new WebUiRequest(enabled),
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/config", null, null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var document = await response.Content.ReadFromJsonAsync<CoreConfigurationDocument>(
            JsonOptions,
            cancellationToken) ?? throw new InvalidDataException("Core returned an empty configuration document.");
        var etag = response.Headers.ETag?.ToString() ?? $"\"{document.Revision}\"";
        return new ConfigurationSnapshot(document, etag);
    }

    public async Task ValidateConfigurationAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await SendJsonAsync<ConfigurationValidationResult>(
            HttpMethod.Post, "api/v1/config/validate", new ConfigurationTextRequest(text), null, cancellationToken);
        if (!result.Valid) throw new InvalidDataException("Core did not confirm that the configuration is valid.");
    }

    public Task<CoreConfigurationSaveResult> SaveConfigurationAsync(
        string text,
        string etag,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            throw new ArgumentException("A configuration ETag is required.", nameof(etag));
        }

        return SendJsonAsync<CoreConfigurationSaveResult>(
            HttpMethod.Put,
            "api/v1/config",
            new ConfigurationTextRequest(text),
            etag,
            cancellationToken);
    }

    public async IAsyncEnumerable<CoreEvent> ReadEventsAsync(
        string? lastEventId,
        Action? onConnected = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/v1/events", null, null);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrWhiteSpace(lastEventId))
        {
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        onConnected?.Invoke();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        string? eventId = null;
        var eventType = "message";
        var data = new StringBuilder();
        var hasData = false;
        var dataOverflow = false;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return new CoreEvent(
                        eventId,
                        eventType,
                        dataOverflow ? "[event payload omitted: over 256 KiB]" : data.ToString(0, data.Length - 1),
                        DateTimeOffset.UtcNow);
                }

                eventId = null;
                eventType = "message";
                data.Clear();
                hasData = false;
                dataOverflow = false;
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var separator = line.IndexOf(':');
            var field = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "id" when !value.Contains('\0'):
                    eventId = value;
                    break;
                case "event":
                    eventType = value.Length == 0 ? "message" : value;
                    break;
                case "data":
                    hasData = true;
                    if (!dataOverflow && data.Length + value.Length + 1 <= 256 * 1024)
                    {
                        data.Append(value).Append('\n');
                    }
                    else
                    {
                        dataOverflow = true;
                    }
                    break;
            }
        }

        if (hasData)
        {
            yield return new CoreEvent(
                eventId,
                eventType,
                dataOverflow ? "[event payload omitted: over 256 KiB]" : data.ToString(0, data.Length - 1),
                DateTimeOffset.UtcNow);
        }
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, path, body, ifMatch, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Core returned an empty response for {path}.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = CreateRequest(method, path, body, ifMatch);
        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body, string? ifMatch)
    {
        Uri endpoint;
        string token;
        lock (_connectionLock)
        {
            endpoint = _endpoint ?? throw new InvalidOperationException("Connect to a Core instance first.");
            token = _adminToken ?? throw new InvalidOperationException("An admin token is required.");
        }

        var request = new HttpRequestMessage(method, new Uri(endpoint, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var code = "HTTP_ERROR";
        var message = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Core request failed." : body;
        try
        {
            var envelope = JsonSerializer.Deserialize<CoreErrorEnvelope>(body, JsonOptions);
            if (envelope?.Error is not null)
            {
                code = envelope.Error.Code;
                message = envelope.Error.Message;
            }
        }
        catch (JsonException)
        {
            // Preserve the original response body for diagnostics when it is not a Core error envelope.
        }

        throw new CoreApiException(response.StatusCode, code, message);
    }

    private sealed record ConfigurationValidationResult(
        [property: JsonPropertyName("valid")] bool Valid);

    private sealed record ConfigurationTextRequest(
        [property: JsonPropertyName("text")] string Text);

    private sealed record WebUiRequest(
        [property: JsonPropertyName("enabled")] bool Enabled);
}
