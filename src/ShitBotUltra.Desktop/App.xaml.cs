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

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
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
        services.AddSingleton<DesktopSettingsStore>();
        services.AddSingleton<CoreProcessManager>();
        services.AddSingleton<CoreEventStreamService>();
        services.AddSingleton<IUiDispatcher>(_ =>
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("The WinUI dispatcher is unavailable.");
            return new WinUiDispatcher(dispatcher);
        });

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ConfigurationViewModel>();
        services.AddSingleton<EventsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddTransient<DashboardPage>();
        services.AddTransient<ConnectionsPage>();
        services.AddTransient<ConfigurationPage>();
        services.AddTransient<EventsPage>();
        services.AddTransient<SettingsPage>();

        return services.BuildServiceProvider();
    }
}
