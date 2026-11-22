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
    private readonly SettingsViewModel _settingsViewModel;
    private readonly LogsViewModel _logsViewModel;
    private readonly CoreLogService _logService;
    private TrayIconService? _trayIcon;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        _viewModel = services.GetRequiredService<MainViewModel>();
        _settingsViewModel = services.GetRequiredService<SettingsViewModel>();
        _logsViewModel = services.GetRequiredService<LogsViewModel>();
        _logService = services.GetRequiredService<CoreLogService>();
        Title = "ShitBotUltra Desktop";

        try
        {
            _trayIcon = new TrayIconService(this);
            _trayIcon.ExitRequested += OnTrayExitRequested;
        }
        catch (Exception exception)
        {
            _logService.Warning("Tray", $"System tray integration is unavailable: {exception.Message}");
            _viewModel.ErrorMessage = "System tray integration is unavailable. The main window can still be closed normally.";
        }

        _settingsViewModel.SettingsApplied += OnSettingsApplied;
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
            "sections" => _services.GetRequiredService<SectionsPage>(),
            "configuration" => _services.GetRequiredService<ConfigurationPage>(),
            "imports" => _services.GetRequiredService<ImportsPage>(),
            "templates" => _services.GetRequiredService<TemplatesPage>(),
            "plugins" => _services.GetRequiredService<PluginsPage>(),
            "events" => _services.GetRequiredService<EventsPage>(),
            "logs" => _services.GetRequiredService<LogsPage>(),
            "updates" => _services.GetRequiredService<UpdatesPage>(),
            "settings" => _services.GetRequiredService<SettingsPage>(),
            _ => _services.GetRequiredService<DashboardPage>()
        };
    }

    private void OnSettingsApplied(DesktopSettings settings)
    {
        if (_trayIcon is null)
        {
            return;
        }

        try
        {
            _trayIcon.MinimizeOnClose = settings.MinimizeToTrayOnClose;
        }
        catch (Exception exception)
        {
            _viewModel.ErrorMessage = $"Could not update the system tray: {exception.Message}";
            _logService.Warning("Tray", exception.Message);
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

        try
        {
            await _viewModel.DisposeAsync();
            _logsViewModel.Dispose();
            await _logService.DisposeAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Desktop shutdown cleanup failed: {exception.Message}");
        }
    }
}
