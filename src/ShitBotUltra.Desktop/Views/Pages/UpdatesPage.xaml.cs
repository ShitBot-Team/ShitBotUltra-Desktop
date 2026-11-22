using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class UpdatesPage : Page
{
    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;
    public UpdatesViewModel ViewModel { get; }
    public UpdatesPage(UpdatesViewModel viewModel) { InitializeComponent(); ViewModel = viewModel; }
    private async void OnCheck(object sender, RoutedEventArgs e) => await ViewModel.CheckAsync();
    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = I18n.Text("Update the connected Core host?"),
            Content = string.Format(I18n.Text("Ask the connected host to verify and install version {0}. It may stop or restart its service. This does not update Desktop."), ViewModel.CandidateVersion),
            PrimaryButtonText = I18n.Text("Ask host to install"), CloseButtonText = I18n.Text("Cancel"),
            DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.InstallAsync();
    }
}
