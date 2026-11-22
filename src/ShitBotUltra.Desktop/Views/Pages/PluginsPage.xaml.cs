using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.Services;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class PluginsPage : Page
{
    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;
    public PluginsViewModel ViewModel { get; }
    public PluginsPage(PluginsViewModel viewModel) { InitializeComponent(); ViewModel = viewModel; }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((ListView)sender).SelectedItem is PluginRow row) ViewModel.SelectedId = row.Id;
    }
    private async Task<bool> Confirm(string title, string content, string action)
    {
        var dialog = new ContentDialog { Title = I18n.Text(title), Content = I18n.Text(content), PrimaryButtonText = I18n.Text(action),
            CloseButtonText = I18n.Text("Cancel"), DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDirty && !await Confirm("Discard plugin draft?", "Loading another plugin replaces the current grant and configuration draft.", "Discard and load")) return;
        await ViewModel.OpenAsync();
    }
    private async void OnImport(object sender, RoutedEventArgs e)
    {
        try { var path = await DesktopFilePicker.PickAsync(".zip"); if (path is not null) await ViewModel.ImportAsync(path); }
        catch (Exception ex) { ViewModel.StatusMessage = ex.Message; }
    }
    private async void OnEnable(object sender, RoutedEventArgs e)
    {
        if (await Confirm("Enable this plugin?", "The selected code will run with the capabilities and scopes shown above.", "Enable")) await ViewModel.EnableAsync();
    }
    private async void OnDisable(object sender, RoutedEventArgs e) => await ViewModel.DisableAsync();
    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDirty && !await Confirm("Discard plugin draft?", "Removing the package discards the current local draft.", "Discard draft")) return;
        if (await Confirm("Remove package?", "Core removes the package and retains its plugin data according to its policy.", "Remove")) await ViewModel.RemoveAsync();
    }
    private async void OnProvider(object sender, RoutedEventArgs e) => await ViewModel.ProviderAsync();
}
