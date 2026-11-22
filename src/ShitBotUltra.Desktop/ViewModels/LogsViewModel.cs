using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private const int MaximumVisibleEntries = 1000;
    private readonly CoreLogService _logService;
    private readonly IUiDispatcher _dispatcher;
    private readonly List<DesktopLogEntry> _allEntries = [];

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string selectedLevel = "All";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public ObservableCollection<DesktopLogEntry> FilteredEntries { get; } = [];

    public string LogFilePath => _logService.LogFilePath;

    public LogsViewModel(CoreLogService logService, IUiDispatcher dispatcher)
    {
        _logService = logService;
        _dispatcher = dispatcher;
        foreach (var entry in _logService.Snapshot().TakeLast(MaximumVisibleEntries))
        {
            _allEntries.Add(entry);
        }

        _logService.EntryReceived += OnEntryReceived;
        RefreshFilter();
    }

    partial void OnFilterTextChanged(string value) => RefreshFilter();

    partial void OnSelectedLevelChanged(string value) => RefreshFilter();

    [RelayCommand]
    private void ClearView()
    {
        _allEntries.Clear();
        FilteredEntries.Clear();
        StatusMessage = "The on-screen history was cleared. The log files remain on disk.";
    }

    [RelayCommand]
    private void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logService.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _logService.LogDirectory,
                UseShellExecute = true
            });
            StatusMessage = "Opened the Desktop log directory.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void OnEntryReceived(DesktopLogEntry entry)
    {
        _dispatcher.Enqueue(() =>
        {
            _allEntries.Add(entry);
            while (_allEntries.Count > MaximumVisibleEntries)
            {
                _allEntries.RemoveAt(0);
            }

            if (Matches(entry))
            {
                FilteredEntries.Add(entry);
                while (FilteredEntries.Count > MaximumVisibleEntries)
                {
                    FilteredEntries.RemoveAt(0);
                }
            }
        });
    }

    private void RefreshFilter()
    {
        FilteredEntries.Clear();
        foreach (var entry in _allEntries.Where(Matches))
        {
            FilteredEntries.Add(entry);
        }
    }

    public void Dispose()
    {
        _logService.EntryReceived -= OnEntryReceived;
    }

    private bool Matches(DesktopLogEntry entry)
    {
        if (!string.Equals(SelectedLevel, "All", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(SelectedLevel, entry.Level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        return entry.Source.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }
}
