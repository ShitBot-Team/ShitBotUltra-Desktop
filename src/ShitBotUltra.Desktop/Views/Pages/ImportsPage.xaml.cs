using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.Services;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class ImportsPage : Page
{
    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;
    public ImportsViewModel ViewModel { get; }
    public ImportsPage(ImportsViewModel viewModel) { InitializeComponent(); ViewModel = viewModel; }
    private void OnSourceSelected(object sender, SelectionChangedEventArgs e)
    { if (((ListView)sender).SelectedItem is ImportSourceRow source) ViewModel.SelectedSourceId = source.Id; }
    private async Task<bool> Confirm(string title, string content, string action)
    {
        var dialog = new ContentDialog { Title = I18n.Text(title), Content = I18n.Text(content), PrimaryButtonText = I18n.Text(action),
            CloseButtonText = I18n.Text("Cancel"), DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.SourcesBelongToCurrentSession && ViewModel.HasSelectedSources &&
            !await Confirm("Change Core source list?", "The selected sources and network mappings belong to the previous Core connection.", "Load current Core")) return;
        await ViewModel.RefreshAsync();
    }
    private async void OnUpload(object sender, RoutedEventArgs e)
    {
        try { var file = await DesktopFilePicker.PickAsync(".db", ".sqlite", ".sqlite3"); if (file is not null) await ViewModel.UploadAsync(file); }
        catch (Exception ex) { ViewModel.StatusMessage = ex.Message; }
    }
    private async void OnDelete(object sender, RoutedEventArgs e)
    { if (await Confirm("Delete staged source?", "Core deletes its uploaded copy. The original file is unchanged.", "Delete copy")) await ViewModel.DeleteSourceAsync(); }
    private async void OnPreview(object sender, RoutedEventArgs e) => await ViewModel.PreviewAsync();
    private async void OnRead(object sender, RoutedEventArgs e) => await ViewModel.ReadReportAsync();
    private async void OnCommit(object sender, RoutedEventArgs e)
    { if (await Confirm("Commit this preview?", "Core will check the preview and create a backup before importing the ready rows.", "Commit")) await ViewModel.CommitAsync(); }
    private async void OnRestore(object sender, RoutedEventArgs e)
    { if (await Confirm("Restore this import?", "Core restores eligible rows as one transaction and rejects conflicting changes.", "Restore")) await ViewModel.RestoreAsync(); }
    private void OnClearExclusions(object sender, RoutedEventArgs e) => ViewModel.ClearExclusions();
    private void OnPrevious(object sender, RoutedEventArgs e) => ViewModel.PreviousPage();
    private void OnNext(object sender, RoutedEventArgs e) => ViewModel.NextPage();
}
