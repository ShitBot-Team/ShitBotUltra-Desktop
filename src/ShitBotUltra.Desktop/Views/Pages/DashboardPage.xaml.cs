using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class DashboardPage : Page
{
    public MainViewModel ViewModel { get; }

    public DashboardPage(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    private async void OnWebUiToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.IsOn != ViewModel.WebUiEnabled)
        {
            toggle.IsOn = ViewModel.WebUiEnabled;
            await ViewModel.ToggleWebUiCommand.ExecuteAsync(null);
            toggle.IsOn = ViewModel.WebUiEnabled;
        }
    }

    private async void OnForceStopClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Force stop Core?",
            Content = "This terminates the Core process tree without waiting for normal shutdown. Use this only when graceful stop fails.",
            PrimaryButtonText = "Force Stop",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                await ViewModel.ForceStopCoreAsync();
            }
            catch (Exception exception)
            {
                ViewModel.ErrorMessage = exception.Message;
            }
        }
    }
}
