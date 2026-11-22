using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class LogsPage : Page
{
    public LogsViewModel ViewModel { get; }

    public LogsPage(LogsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
}
