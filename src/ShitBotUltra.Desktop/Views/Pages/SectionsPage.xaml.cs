using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class SectionsPage : Page
{
    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;
    public SectionsViewModel ViewModel { get; }
    public SectionsPage(SectionsViewModel viewModel)
    {
        InitializeComponent(); ViewModel = viewModel;
        Unloaded += (_, _) => { ViewModel.CreatedSecret = ""; SecretBox.Password = ""; };
    }
    private async void OnLoad(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDirty)
        {
            var dialog = new ContentDialog { Title = I18n.Text("Discard section draft?"), Content = I18n.Text("Reloading replaces the unsaved section and binding form edits."),
                PrimaryButtonText = I18n.Text("Discard and reload"), CloseButtonText = I18n.Text("Keep draft"), DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }
        await ViewModel.LoadAsync();
    }
    private void OnApplyForm(object sender, RoutedEventArgs e) => ViewModel.ApplyBindingForm();
    private async void OnSave(object sender, RoutedEventArgs e) => await ViewModel.SaveAsync();
    private async void OnCreateSecret(object sender, RoutedEventArgs e)
    {
        var value = SecretBox.Password;
        SecretBox.Password = "";
        await ViewModel.CreateSecretAsync(value);
        if (!IsLoaded) ViewModel.CreatedSecret = "";
    }
    private void OnClearSecret(object sender, RoutedEventArgs e) { ViewModel.CreatedSecret = ""; SecretBox.Password = ""; }
}
