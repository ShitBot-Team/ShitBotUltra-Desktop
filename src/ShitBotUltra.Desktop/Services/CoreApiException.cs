using System.Net;

namespace shitbotteam.ultra.desktop.Services;

public sealed class CoreApiException : Exception
{
    public CoreApiException(HttpStatusCode statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode StatusCode { get; }

    public string Code { get; }
}
