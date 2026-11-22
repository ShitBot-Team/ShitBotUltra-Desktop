using System.Text.Json;
using System.Text.Json.Serialization;

namespace shitbotteam.ultra.desktop.Models;

public sealed record CoreStatus
{
    [JsonPropertyName("component")]
    public string Component { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("admin_api_version")]
    public int AdminApiVersion { get; init; }

    [JsonPropertyName("bridge_protocol_version")]
    public int BridgeProtocolVersion { get; init; }

    [JsonPropertyName("instance_id")]
    public string InstanceId { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = "unknown";

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    [JsonPropertyName("nodes")]
    public List<JsonElement> Nodes { get; init; } = [];

    [JsonPropertyName("adapters")]
    public List<JsonElement> Adapters { get; init; } = [];

    [JsonPropertyName("last_error")]
    public string? LastError { get; init; }

    [JsonPropertyName("webui_enabled")]
    public bool WebUiEnabled { get; init; }
}

public sealed record PlayerCounts
{
    [JsonPropertyName("players")]
    public int Players { get; init; }

    [JsonPropertyName("online")]
    public int Online { get; init; }

    [JsonPropertyName("bound")]
    public int Bound { get; init; }

    [JsonPropertyName("whitelist_only")]
    public int WhitelistOnly { get; init; }

    [JsonPropertyName("unlisted")]
    public int Unlisted { get; init; }
}

public sealed record DashboardSnapshot
{
    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; init; }

    [JsonPropertyName("counts")]
    public PlayerCounts Counts { get; init; } = new();

    [JsonPropertyName("networks")]
    public List<JsonElement> Networks { get; init; } = [];

    [JsonPropertyName("nodes")]
    public int Nodes { get; init; }

    [JsonPropertyName("connected_nodes")]
    public int ConnectedNodes { get; init; }

    [JsonPropertyName("adapters")]
    public int Adapters { get; init; }

    [JsonPropertyName("enabled_adapters")]
    public int EnabledAdapters { get; init; }

    [JsonPropertyName("connected_adapters")]
    public int ConnectedAdapters { get; init; }

    [JsonPropertyName("routes")]
    public int Routes { get; init; }

    [JsonPropertyName("commands")]
    public int Commands { get; init; }
}

public sealed record CoreConnectionsSnapshot
{
    [JsonPropertyName("nodes")]
    public List<JsonElement> Nodes { get; init; } = [];

    [JsonPropertyName("adapters")]
    public List<JsonElement> Adapters { get; init; } = [];
}



public sealed record CoreConfigurationDocument
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("revision")]
    public string Revision { get; init; } = string.Empty;
}

public sealed record CoreConfigurationSaveResult
{
    [JsonPropertyName("document")]
    public CoreConfigurationDocument Document { get; init; } = new();

    [JsonPropertyName("restart_required")]
    public bool RestartRequired { get; init; }
}

public sealed record CoreErrorEnvelope
{
    [JsonPropertyName("error")]
    public CoreError? Error { get; init; }
}

public sealed record CoreError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "CORE_ERROR";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "The Core request failed.";
}

public sealed record CoreEvent(
    string? Id,
    string Type,
    string Data,
    DateTimeOffset ReceivedAt);

public sealed record ConnectionRow(
    string Category,
    string Name,
    string State,
    string Details)
{
    public static ConnectionRow FromNode(JsonElement node) => new(
        "Node",
        ReadString(node, "id", "unknown"),
        ReadString(node, "state", ReadBoolean(node, "connected") ? "connected" : "disconnected"),
        JoinDetails(node, "network_id", "server_id", "address"));

    public static ConnectionRow FromAdapter(JsonElement adapter) => new(
        "Adapter",
        ReadString(adapter, "id", ReadString(adapter, "name", ReadString(adapter, "kind", "unknown"))),
        ReadString(adapter, "state", "unknown"),
        JoinDetails(adapter, "protocol", "kind", "account", "last_error"));

    private static string ReadString(JsonElement element, string name, string fallback)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return fallback;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
        }

        return fallback;
    }

    private static bool ReadBoolean(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static string JoinDetails(JsonElement element, params string[] names)
    {
        var values = names
            .Select(name => ReadString(element, name, string.Empty))
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(" | ", values);
    }
}

public sealed record CoreEventRow(
    string Id,
    string Type,
    string Summary,
    DateTimeOffset ReceivedAt)
{
    public static CoreEventRow From(CoreEvent value)
    {
        var summary = value.Data;
        if (summary.Length > 280)
        {
            summary = string.Concat(summary[..277], "...");
        }

        var id = value.Id ?? string.Concat(value.Type, ":", value.ReceivedAt.ToUnixTimeMilliseconds());
        return new CoreEventRow(id, value.Type, summary, value.ReceivedAt);
    }
}
