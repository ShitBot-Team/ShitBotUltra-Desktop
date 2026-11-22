using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class SectionsViewModel : ObservableObject
{
    private static readonly string[] Names = ["connections", "nodes", "routes", "commands", "tasks", "binding", "rendering"];
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private readonly CoreApiClient _api;
    private long _session = -1;
    private string _revision = "";
    private string _savedText = "";
    private string _savedForm = "";
    private JsonObject? _baseline;
    [ObservableProperty] private string sectionsText = "";
    [ObservableProperty] private bool bindingEnabled;
    [ObservableProperty] private bool kickOnUnbind;
    [ObservableProperty] private string ttl = "300";
    [ObservableProperty] private string attempts = "5";
    [ObservableProperty] private string attemptWindow = "600";
    [ObservableProperty] private string maxPlayers = "3";
    [ObservableProperty] private string secretName = "";
    [ObservableProperty] private string createdSecret = "";
    [ObservableProperty] private string statusMessage = "Connect to Core and load configuration sections.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    public bool CanEdit => !IsBusy;
    public bool FormDirty => _baseline is not null && FormFingerprint() != _savedForm;
    public bool IsDirty => _baseline is not null && (SectionsText != _savedText || FormDirty);
    public SectionsViewModel(CoreApiClient api) => _api = api;
    private string FormFingerprint() => JsonSerializer.Serialize(new[] { BindingEnabled.ToString(), KickOnUnbind.ToString(), Ttl, Attempts, AttemptWindow, MaxPlayers });

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { StatusMessage = $"{ex.Message} Your draft was kept; reload and merge if Core reports a revision conflict."; }
        finally { IsBusy = false; }
    }
    public Task LoadAsync() => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var result = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/config/sections", session)
            ?? throw new InvalidDataException("Core returned no configuration sections.");
        _session = session; Receive(result);
        StatusMessage = "Sections loaded. Core validates all changed sections together and checks the revision.";
    });
    private void Receive(JsonElement result)
    {
        _revision = result.GetProperty("revision").GetString() ?? "";
        var config = JsonNode.Parse(result.GetProperty("config").GetRawText())?.AsObject()
            ?? throw new InvalidDataException("Core returned invalid configuration sections.");
        var sections = new JsonObject();
        foreach (var name in Names)
            sections[name] = config[name]?.DeepClone() ?? (name is "binding" or "rendering" ? (JsonNode)new JsonObject() : new JsonArray());
        _baseline = (JsonObject)sections.DeepClone();
        SectionsText = sections.ToJsonString(Pretty); _savedText = SectionsText;
        ReadBindingForm(sections);
    }
    private void ReadBindingForm(JsonObject sections)
    {
        var binding = sections["binding"]?.AsObject() ?? throw new InvalidDataException("Binding section must be an object.");
        BindingEnabled = binding["enabled"]?.GetValue<bool>() ?? false;
        KickOnUnbind = binding["kick_on_unbind"]?.GetValue<bool>() ?? false;
        Ttl = binding["code_ttl_seconds"]?.ToString() ?? "";
        Attempts = binding["max_attempts"]?.ToString() ?? "";
        AttemptWindow = binding["attempt_window_seconds"]?.ToString() ?? "";
        MaxPlayers = binding["max_players_per_identity"]?.ToString() ?? "";
        _savedForm = FormFingerprint();
    }
    public void ApplyBindingForm()
    {
        try
        {
            RequireCurrent();
            var sections = ParseSections();
            var binding = sections["binding"]?.AsObject() ?? throw new InvalidDataException("Binding section must be an object.");
            binding["enabled"] = BindingEnabled; binding["kick_on_unbind"] = KickOnUnbind;
            binding["code_ttl_seconds"] = ParseInteger(Ttl); binding["max_attempts"] = ParseInteger(Attempts);
            binding["attempt_window_seconds"] = ParseInteger(AttemptWindow); binding["max_players_per_identity"] = ParseInteger(MaxPlayers);
            SectionsText = sections.ToJsonString(Pretty); _savedForm = FormFingerprint();
            StatusMessage = "Binding form copied into the section draft. Save sections to apply it through Core.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }
    public Task SaveAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        if (FormDirty) throw new InvalidOperationException("Apply the binding form to the section draft before saving.");
        var current = ParseSections();
        var changes = new JsonObject();
        foreach (var name in Names)
            if (!JsonNode.DeepEquals(current[name], _baseline![name])) changes[name] = current[name]?.DeepClone();
        if (changes.Count == 0) { StatusMessage = "No sections have changed."; return; }
        var result = await _api.ManagementRequestAsync(HttpMethod.Patch, "api/v1/config/sections", _session, changes, _revision)
            ?? throw new InvalidDataException("Core returned no saved configuration.");
        Receive(result);
        var restart = result.TryGetProperty("restart_required", out var flag) && flag.ValueKind == JsonValueKind.True;
        StatusMessage = restart ? "Sections saved. Restart the Core host to activate the changes." : "Sections saved.";
    });
    public Task CreateSecretAsync(string value) => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var result = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/secrets", session,
            new { name = SecretName, value }) ?? throw new InvalidDataException("Core returned no credential.");
        CreatedSecret = "Reference: " + result.GetProperty("reference").GetString() + "\nValue: " + result.GetProperty("value").GetString();
        StatusMessage = "Credential created by Core. Copy it if required, then clear it from this view.";
    });
    private JsonObject ParseSections()
    {
        var result = JsonNode.Parse(SectionsText)?.AsObject() ?? throw new InvalidDataException("Sections must be a JSON object.");
        if (result.Count != Names.Length || Names.Any(name => !result.ContainsKey(name)) || result.Any(p => !Names.Contains(p.Key)))
            throw new InvalidDataException("Keep the seven original section names; Core saves only the sections you change.");
        return result;
    }
    private static int ParseInteger(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value : throw new InvalidDataException("Binding limits must be whole numbers.");
    private void RequireCurrent()
    {
        if (_baseline is null || string.IsNullOrWhiteSpace(_revision) || _session != _api.ConnectionVersion)
            throw new InvalidOperationException("Load sections for the current Core connection first.");
    }
}
