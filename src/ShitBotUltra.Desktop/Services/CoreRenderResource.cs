using System.Net.Http.Headers;

namespace shitbotteam.ultra.desktop.Services;

public sealed partial class CoreApiClient
{
    public async Task<byte[]> ReadRenderImageAsync(string id, long expectedSession, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        HttpRequestMessage request;
        lock (_connectionLock)
        {
            if (_connectionVersion != expectedSession)
                throw new InvalidOperationException("The Core connection changed. Reload the template.");
            request = CreateRequest(HttpMethod.Get, "api/v1/renders/resources/" + ManagementSegment(id), null, null);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
        }
        using (request)
        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
        {
            await response.Content.LoadIntoBufferAsync(16L * 1024 * 1024, timeout.Token);
            await EnsureSuccessAsync(response, timeout.Token);
            if (response.Content.Headers.ContentType?.MediaType is not ("image/png" or "image/jpeg"))
                throw new InvalidDataException("Core returned an unsupported image type.");
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            if (ConnectionVersion != expectedSession)
                throw new InvalidOperationException("The Core connection changed while loading the preview.");
            return bytes;
        }
    }
}
