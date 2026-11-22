using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class ConfigurationPage : Page
{
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
            Title = "Discard configuration draft?",
            Content = "The current YAML draft has not been saved. Reloading replaces it with the latest Core configuration.",
            PrimaryButtonText = "Discard and reload",
            CloseButtonText = "Keep draft",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ReloadDiscardAsync();
        }
    }
}
