using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using shitbotteam.ultra.desktop.Services;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    public shitbotteam.ultra.desktop.Services.LocalizationService I18n => shitbotteam.ultra.desktop.Services.LocalizationService.Current;
    public TemplatesViewModel ViewModel { get; }
    private string _selectedFile = "";
    public TemplatesPage(TemplatesViewModel viewModel) { InitializeComponent(); ViewModel = viewModel; }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (((ListView)sender).SelectedItem is TemplateRow row) ViewModel.SelectedId = row.Id; }
    private void OnFileSelected(object sender, SelectionChangedEventArgs e)
    { _selectedFile = ((ListView)sender).SelectedItem as string ?? ""; }
    private async Task<bool> Confirm(string title, string content, string action)
    {
        var dialog = new ContentDialog { Title = I18n.Text(title), Content = I18n.Text(content), PrimaryButtonText = I18n.Text(action),
            CloseButtonText = I18n.Text("Cancel"), DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDirty && !await Confirm("Discard template draft?", "Loading another template replaces the current unsaved file and style edits.", "Discard and load")) return;
        await ViewModel.OpenAsync();
    }
    private async void OnImport(object sender, RoutedEventArgs e)
    {
        try { var path = await DesktopFilePicker.PickAsync(".zip"); if (path is not null) await ViewModel.ImportAsync(path); }
        catch (Exception ex) { ViewModel.StatusMessage = ex.Message; }
    }
    private async void OnLoadFiles(object sender, RoutedEventArgs e)
    {
        if (ViewModel.FileDirty && !await Confirm("Discard file draft?", "Loading another version replaces the current file draft.", "Discard and load")) return;
        await ViewModel.LoadFilesAsync();
    }
    private async void OnOpenFile(object sender, RoutedEventArgs e)
    {
        if (ViewModel.FileDirty && !await Confirm("Discard file draft?", "Reading a file replaces the current file draft.", "Discard and read")) return;
        var path = string.IsNullOrEmpty(_selectedFile) ? ViewModel.FilePath : _selectedFile;
        await ViewModel.OpenFileAsync(path);
    }
    private async void OnSaveFile(object sender, RoutedEventArgs e) => await ViewModel.SaveFileAsync(false);
    private async void OnDeleteFile(object sender, RoutedEventArgs e)
    { if (await Confirm("Delete this file from a new draft?", "The published version is unchanged until a version is published.", "Delete")) await ViewModel.SaveFileAsync(true); }
    private async void OnDefaults(object sender, RoutedEventArgs e) => await ViewModel.SaveDefaultsAsync();
    private async void OnPublish(object sender, RoutedEventArgs e)
    { if (await Confirm("Publish selected version?", "The selected saved version becomes the published template.", "Publish")) await ViewModel.PublishAsync(false); }
    private async void OnRestore(object sender, RoutedEventArgs e)
    { if (await Confirm("Restore built-in template?", "Core will restore the built-in published version; saved drafts remain available.", "Restore")) await ViewModel.PublishAsync(true); }
    private async void OnProvider(object sender, RoutedEventArgs e) => await ViewModel.ProviderAsync();
    private async void OnRender(object sender, RoutedEventArgs e)
    {
        var bytes = await ViewModel.RenderAsync();
        if (bytes is null) return;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            { writer.WriteBytes(bytes); await writer.StoreAsync(); await writer.FlushAsync(); }
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            PreviewImage.Source = bitmap;
        }
        catch (Exception ex) { ViewModel.StatusMessage = $"Could not display the preview: {ex.Message}"; }
    }
}
