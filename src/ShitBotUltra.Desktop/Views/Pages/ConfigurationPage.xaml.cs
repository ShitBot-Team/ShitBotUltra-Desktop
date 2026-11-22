using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class ConfigurationPage : Page
{
    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;
    public ConfigurationViewModel ViewModel { get; }

    public ConfigurationPage(ConfigurationViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void OnReloadDiscardClicked(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsDirty)
        {
            await ViewModel.ReloadDiscardAsync();
            return;
        }

        var dialog = new ContentDialog
        {
            Title = I18n.Text("Discard configuration draft?"),
            Content = I18n.Text("The current YAML draft has not been saved. Reloading replaces it with the latest Core configuration."),
            PrimaryButtonText = I18n.Text("Discard and reload"),
            CloseButtonText = I18n.Text("Keep draft"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ReloadDiscardAsync();
        }
    }
}
