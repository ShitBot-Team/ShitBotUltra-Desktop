using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class UpdatesPage : Page
{
    public UpdatesViewModel ViewModel { get; }
    public UpdatesPage(UpdatesViewModel viewModel) { InitializeComponent(); ViewModel = viewModel; }
    private async void OnCheck(object sender, RoutedEventArgs e) => await ViewModel.CheckAsync();
    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Update the connected Core host?",
            Content = $"Ask the connected host to verify and install version {ViewModel.CandidateVersion}. It may stop or restart its service. This does not update Desktop.",
            PrimaryButtonText = "Ask host to install", CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.InstallAsync();
    }
}
