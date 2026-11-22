using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public sealed record TemplateRow(string Id, string Published);

public partial class TemplatesViewModel : ObservableObject
{
    private readonly CoreApiClient _api;
    private long _session = -1;
    private string _revision = "";
    private string _savedFile = "";
    private string _savedStyle = "";
    private string _fileVersion = "";
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    [ObservableProperty] private string selectedId = "";
    [ObservableProperty] private string currentId = "";
    [ObservableProperty] private string versionRevision = "";
    [ObservableProperty] private string filePath = "";
    [ObservableProperty] private string fileText = "";
    [ObservableProperty] private string theme = "{}";
    [ObservableProperty] private string css = "";
    [ObservableProperty] private string locale = "zh-CN";
    [ObservableProperty] private string metadata = "";
    [ObservableProperty] private string providerName = "core.online";
    [ObservableProperty] private string providerRequest = "{\"network_id\":\"\",\"player_name\":\"\",\"server_id\":\"\"}";
    [ObservableProperty] private string renderData = "{}";
    [ObservableProperty] private string statusMessage = "Connect to Core and refresh the template list.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;

    public ObservableCollection<TemplateRow> Templates { get; } = [];
    public ObservableCollection<string> Versions { get; } = [];
    public ObservableCollection<string> Files { get; } = [];
    public bool CanEdit => !IsBusy;
    public bool FileDirty => !string.IsNullOrEmpty(CurrentId) && FileDraft() != _savedFile;
    public bool IsDirty => FileDirty || (!string.IsNullOrEmpty(CurrentId) && StyleDraft() != _savedStyle);
    private string FileDraft() => JsonSerializer.Serialize(new[] { FilePath, FileText });
    private string StyleDraft() => JsonSerializer.Serialize(new[] { Theme, Css, Locale });
    public TemplatesViewModel(CoreApiClient api) => _api = api;

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { StatusMessage = $"{ex.Message} Refresh the Core state before retrying a change."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        await RefreshListAsync(_api.ConnectionVersion);
        StatusMessage = "Template list refreshed.";
    });

    private async Task RefreshListAsync(long session)
    {
        var data = await _api.ManagementRequestAsync(HttpMethod.Get, "api/v1/templates", session)
            ?? throw new InvalidDataException("Core returned no template list.");
        Templates.Clear();
        foreach (var item in data.GetProperty("items").EnumerateArray())
            Templates.Add(new TemplateRow(item.GetProperty("id").GetString() ?? "", item.GetProperty("published").GetString() ?? ""));
    }

    public Task ImportAsync(string file) => RunAsync(async () =>
    {
        var session = _api.ConnectionVersion;
        var value = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/templates", session, uploadPath: file)
            ?? throw new InvalidDataException("Core returned no template details.");
        await RefreshListAsync(session);
        SelectedId = value.GetProperty("id").GetString() ?? "";
        StatusMessage = "Template imported. Load it to inspect versions and preview before publishing.";
    });

    public Task OpenAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(SelectedId)) throw new InvalidOperationException("Select a template first.");
        var session = _api.ConnectionVersion;
        var value = await _api.ManagementRequestAsync(HttpMethod.Get, Path(SelectedId), session)
            ?? throw new InvalidDataException("Core returned no template details.");
        _session = session;
        ApplyMetadata(value);
        var draft = value.GetProperty("draft").GetString();
        var published = value.GetProperty("published").GetString();
        VersionRevision = !string.IsNullOrEmpty(draft) ? draft : !string.IsNullOrEmpty(published) ? published : Versions.FirstOrDefault() ?? "";
        var defaults = value.GetProperty("defaults");
        Css = defaults.TryGetProperty("css", out var cssValue) ? cssValue.GetString() ?? "" : "";
        Locale = defaults.TryGetProperty("locale", out var localeValue) ? localeValue.GetString() ?? "zh-CN" : "zh-CN";
        Theme = defaults.TryGetProperty("theme", out var themeValue) && themeValue.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Serialize(themeValue, Pretty) : "{}";
        FilePath = ""; FileText = ""; _savedFile = FileDraft(); _savedStyle = StyleDraft();
        _fileVersion = VersionRevision;
        await LoadFilesCoreAsync();
        StatusMessage = "Template loaded. Changes are drafts until you explicitly publish.";
    });

    public Task LoadFilesAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        await LoadFilesCoreAsync();
        FilePath = ""; FileText = ""; _savedFile = FileDraft(); _fileVersion = VersionRevision;
        StatusMessage = "File list loaded for the selected version.";
    });
    private async Task LoadFilesCoreAsync()
    {
        RequireCurrent();
        var value = await _api.ManagementRequestAsync(HttpMethod.Get,
            Path(CurrentId) + "/files?revision=" + Uri.EscapeDataString(VersionRevision), _session)
            ?? throw new InvalidDataException("Core returned no file list.");
        Files.Clear();
        foreach (var item in value.GetProperty("items").EnumerateArray()) Files.Add(item.GetString() ?? "");
    }
    public Task OpenFileAsync(string path) => RunAsync(async () =>
    {
        RequireCurrent();
        var value = await _api.ManagementRequestAsync(HttpMethod.Get,
            Path(CurrentId) + "/file?revision=" + Uri.EscapeDataString(VersionRevision) + "&path=" + Uri.EscapeDataString(path), _session)
            ?? throw new InvalidDataException("Core returned no file.");
        FilePath = path; FileText = value.GetProperty("text").GetString() ?? ""; _savedFile = FileDraft(); _fileVersion = VersionRevision;
        StatusMessage = "File loaded; editing affects a local draft only until saved.";
    });

    public Task SaveFileAsync(bool delete) => RunAsync(async () =>
    {
        RequireCurrent();
        if (string.IsNullOrWhiteSpace(FilePath)) throw new InvalidOperationException("Enter a template file path first.");
        if (_fileVersion != VersionRevision) throw new InvalidOperationException("Load the selected version files before editing its draft.");
        var files = new Dictionary<string, string?> { [FilePath] = delete ? null : FileText };
        var body = new { base_revision = VersionRevision, files };
        var result = await _api.ManagementRequestAsync(HttpMethod.Put, Path(CurrentId) + "/draft", _session, body, _revision)
            ?? throw new InvalidDataException("Core returned no template details.");
        ApplyMetadata(result);
        VersionRevision = result.GetProperty("draft").GetString() ?? "";
        _fileVersion = VersionRevision;
        if (delete) { FilePath = ""; FileText = ""; }
        _savedFile = FileDraft();
        await LoadFilesCoreAsync();
        await RefreshListAsync(_session);
        StatusMessage = "New draft saved. The published version was not changed.";
    });

    public Task SaveDefaultsAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        using var themeDocument = JsonDocument.Parse(Theme);
        if (themeDocument.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Theme must be a JSON object.");
        var body = new { theme = themeDocument.RootElement.Clone(), css = Css, locale = Locale };
        var result = await _api.ManagementRequestAsync(HttpMethod.Put, Path(CurrentId) + "/defaults", _session, body, _revision)
            ?? throw new InvalidDataException("Core returned no template details.");
        ApplyMetadata(result); _savedStyle = StyleDraft();
        StatusMessage = "Default style saved. Unsaved file edits were kept.";
    });

    public Task PublishAsync(bool restore) => RunAsync(async () =>
    {
        RequireCurrent();
        object? body = restore ? null : new { version_revision = VersionRevision };
        var result = await _api.ManagementRequestAsync(HttpMethod.Post,
            Path(CurrentId) + (restore ? "/restore" : "/publish"), _session, body, _revision)
            ?? throw new InvalidDataException("Core returned no template details.");
        ApplyMetadata(result); await RefreshListAsync(_session);
        StatusMessage = restore ? "Built-in version restored; local edits were kept." : "Selected version published; local edits were kept.";
    });

    public Task ProviderAsync() => RunAsync(async () =>
    {
        RequireCurrent();
        using var input = JsonDocument.Parse(ProviderRequest);
        var result = await _api.ManagementRequestAsync(HttpMethod.Post,
            "api/v1/rendering/providers/" + CoreApiClient.ManagementSegment(ProviderName), _session, input.RootElement.Clone());
        RenderData = result is null ? "{}" : JsonSerializer.Serialize(result.Value, Pretty);
        StatusMessage = "Provider data loaded into the preview input.";
    });

    public async Task<byte[]?> RenderAsync()
    {
        byte[]? image = null;
        await RunAsync(async () =>
        {
            RequireCurrent();
            using var input = JsonDocument.Parse(RenderData);
            var result = await _api.ManagementRequestAsync(HttpMethod.Post, "api/v1/renders", _session,
                new { template_id = CurrentId, revision = VersionRevision, data = input.RootElement.Clone() })
                ?? throw new InvalidDataException("Core returned no render resource.");
            image = await _api.ReadRenderImageAsync(result.GetProperty("resource_id").GetString() ?? "", _session);
            StatusMessage = "Preview rendered by Core.";
        });
        return image;
    }

    private void ApplyMetadata(JsonElement value)
    {
        CurrentId = value.GetProperty("id").GetString() ?? "";
        _revision = value.GetProperty("revision").GetString() ?? "";
        var selectedVersion = VersionRevision;
        Versions.Clear();
        foreach (var item in value.GetProperty("versions").EnumerateArray()) Versions.Add(item.GetProperty("revision").GetString() ?? "");
        VersionRevision = selectedVersion ?? "";
        Metadata = JsonSerializer.Serialize(value, Pretty);
    }
    private void RequireCurrent()
    {
        if (string.IsNullOrWhiteSpace(CurrentId) || string.IsNullOrWhiteSpace(_revision)) throw new InvalidOperationException("Load a template first.");
        if (_session != _api.ConnectionVersion) throw new InvalidOperationException("The Core connection changed. Keep your draft and reload the template.");
    }
    private static string Path(string id) => "api/v1/templates/" + CoreApiClient.ManagementSegment(id);
}
