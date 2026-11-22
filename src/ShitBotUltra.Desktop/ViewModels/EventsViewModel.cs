using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using shitbotteam.ultra.desktop.Models;
using shitbotteam.ultra.desktop.Services;

namespace shitbotteam.ultra.desktop.ViewModels;

public partial class EventsViewModel : ObservableObject
{
    private const int MaximumVisibleEvents = 500;
    private readonly CoreEventStreamService _eventStream;
    private readonly IUiDispatcher _dispatcher;
    private readonly HashSet<string> _seenEventIds = new(StringComparer.Ordinal);

    [ObservableProperty]
    private string streamState = "Disconnected";

    public ObservableCollection<CoreEventRow> Events { get; } = [];

    public EventsViewModel(CoreEventStreamService eventStream, IUiDispatcher dispatcher)
    {
        _eventStream = eventStream;
        _dispatcher = dispatcher;
        _eventStream.EventReceived += OnEventReceived;
        _eventStream.StateChanged += OnStateChanged;
        _eventStream.HistoryCleared += OnHistoryCleared;
        foreach (var value in _eventStream.Snapshot().Reverse())
        {
            AddEvent(CoreEventRow.From(value));
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Events.Clear();
        _seenEventIds.Clear();
    }

    private void OnEventReceived(CoreEvent value)
    {
        var row = CoreEventRow.From(value);
        _dispatcher.Enqueue(() => AddEvent(row));
    }

    private void AddEvent(CoreEventRow row)
    {
        if (!_seenEventIds.Add(row.Id))
        {
            return;
        }

        Events.Insert(0, row);
        while (Events.Count > MaximumVisibleEvents)
        {
            _seenEventIds.Remove(Events[^1].Id);
            Events.RemoveAt(Events.Count - 1);
        }
    }

    private void OnStateChanged(string value)
    {
        _dispatcher.Enqueue(() => StreamState = value);
    }

    private void OnHistoryCleared()
    {
        _dispatcher.Enqueue(() =>
        {
            Events.Clear();
            _seenEventIds.Clear();
        });
    }
}
