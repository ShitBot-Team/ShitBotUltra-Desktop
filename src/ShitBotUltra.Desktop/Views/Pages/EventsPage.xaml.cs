using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class EventsPage : Page
{
    public EventsViewModel ViewModel { get; }

    public EventsPage(EventsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
}
