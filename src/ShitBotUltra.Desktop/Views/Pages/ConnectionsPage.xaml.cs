using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class ConnectionsPage : Page
{
    public MainViewModel ViewModel { get; }

    public ConnectionsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
}
