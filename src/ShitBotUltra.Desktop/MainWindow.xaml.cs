using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.Models;
using shitbotteam.ultra.desktop.Services;
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
        await _settingsViewModel.LoadAsync();
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

    private void OnSettingsApplied(DesktopSettings settings)
    {
        try
        {
            if (_trayIcon is not null)
            {
                _trayIcon.MinimizeOnClose = settings.MinimizeToTrayOnClose;
            }
        }
        catch (Exception exception)
        {
            _viewModel.ErrorMessage = $"Could not update the system tray: {exception.Message}";
        }
    }

    private void OnTrayExitRequested()
    {
        _trayIcon?.AllowClose();
        Close();
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        _settingsViewModel.SettingsApplied -= OnSettingsApplied;
        if (_trayIcon is not null)
        {
            _trayIcon.ExitRequested -= OnTrayExitRequested;
            _trayIcon.Dispose();
        }

        await _viewModel.DisposeAsync();
    }
}
