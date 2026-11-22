using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using shitbotteam.ultra.desktop.Services;
using shitbotteam.ultra.desktop.ViewModels;
using shitbotteam.ultra.desktop.Views.Pages;

namespace shitbotteam.ultra.desktop;

public partial class App : Application
{
    private static IServiceProvider Services { get; set; } = null!;
    private Window? _window;

    public static IntPtr MainWindowHandle { get; private set; }

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            CheckCertificateRevocationList = true
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddSingleton<CoreApiClient>();
        services.AddSingleton<CoreLogService>();
        services.AddSingleton<DesktopSettingsStore>();
        services.AddSingleton<CoreProcessManager>();
        services.AddSingleton<CoreEventStreamService>();
        services.AddSingleton<IUiDispatcher>(_ =>
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("The WinUI dispatcher is unavailable.");
            return new WinUiDispatcher(dispatcher);
        });

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ConfigurationViewModel>();
        services.AddSingleton<EventsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<PluginsViewModel>();
        services.AddSingleton<TemplatesViewModel>();
        services.AddSingleton<ImportsViewModel>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<SectionsViewModel>();

        services.AddTransient<DashboardPage>();
        services.AddTransient<ConnectionsPage>();
        services.AddTransient<ConfigurationPage>();
        services.AddTransient<EventsPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<LogsPage>();
        services.AddTransient<PluginsPage>();
        services.AddTransient<TemplatesPage>();
        services.AddTransient<ImportsPage>();
        services.AddTransient<UpdatesPage>();
        services.AddTransient<SectionsPage>();

        return services.BuildServiceProvider();
    }
}
