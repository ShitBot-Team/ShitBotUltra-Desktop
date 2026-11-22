using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;
using shitbotteam.ultra.desktop.Views.Pages;

namespace shitbotteam.ultra.desktop;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly MainViewModel _viewModel;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        _viewModel = services.GetRequiredService<MainViewModel>();
        Title = "ShitBotUltra Desktop";

        RootNavigation.SelectedItem = DashboardItem;
        ShowPage("overview");
        RootNavigation.Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string pageId)
        {
            ShowPage(pageId);
        }
    }

    private void ShowPage(string pageId)
    {
        ContentFrame.Content = pageId switch
        {
            "overview" => _services.GetRequiredService<DashboardPage>(),
            "connections" => _services.GetRequiredService<ConnectionsPage>(),
            "configuration" => _services.GetRequiredService<ConfigurationPage>(),
            "events" => _services.GetRequiredService<EventsPage>(),
            "settings" => _services.GetRequiredService<SettingsPage>(),
            _ => _services.GetRequiredService<DashboardPage>()
        };
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        await _viewModel.DisposeAsync();
    }
}
