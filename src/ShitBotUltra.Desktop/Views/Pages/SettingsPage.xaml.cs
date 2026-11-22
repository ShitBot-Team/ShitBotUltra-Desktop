using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AdminTokenBox.Password = ViewModel.TokenInput;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.TokenInput = AdminTokenBox.Password;
    }

    private async void OnConnectClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.ConnectCommand.ExecuteAsync(null);
        AdminTokenBox.Password = ViewModel.TokenInput;
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
            Title = I18n.Text("Discard Desktop preferences?"),
            Content = I18n.Text("The current endpoint or CLI path has not been saved. Reloading replaces these values with the saved preferences."),
            PrimaryButtonText = I18n.Text("Discard and reload"),
            CloseButtonText = I18n.Text("Keep changes"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ReloadDiscardAsync();
        }
    }
}
