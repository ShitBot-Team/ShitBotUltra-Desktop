namespace shitbotteam.ultra.desktop.Services;

public static class CoreEndpoint
{
    public static Uri Parse(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var endpoint))
        {
            throw new ArgumentException("Enter an absolute Core URL.", nameof(value));
        }

        if ((endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || endpoint.AbsolutePath is not ("/" or ""))
        {
            throw new ArgumentException("The Core URL must contain only an HTTP(S) scheme, host and optional port.", nameof(value));
        }

        if (!endpoint.IsLoopback && endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Remote Core connections must use HTTPS.", nameof(value));
        }

        return new UriBuilder(endpoint) { Path = "/" }.Uri;
    }

    public static string Normalize(string value) => Parse(value).AbsoluteUri;
}
