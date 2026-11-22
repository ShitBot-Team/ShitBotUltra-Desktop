using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using shitbotteam.ultra.desktop.ViewModels;

namespace shitbotteam.ultra.desktop.Views.Pages;

public sealed partial class TasksPage : Page
{
    public TasksViewModel ViewModel { get; }
    public TasksPage(TasksViewModel viewModel) { InitializeComponent(); ViewModel = viewModel; }
    private void OnSelected(object sender, SelectionChangedEventArgs e)
    { if (((ListView)sender).SelectedItem is ScheduledTaskRow item) ViewModel.SelectedId = item.Id; }
    private async void OnRefresh(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();
    private async void OnQuery(object sender, RoutedEventArgs e) => await ViewModel.QueryPendingAsync();
    private async void OnRun(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { Title = "Run the selected task once?", Content = "This can send a message or execute a game action. It does not change the next scheduled time.",
            PrimaryButtonText = "Run once", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.ExecuteAsync();
    }
    private async void OnAcknowledge(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasUncertainResult) return;
        var dialog = new ContentDialog { Title = "Release the previous request?", Content = "Confirm that you checked the target and know whether the previous action happened. Releasing it permits another execution, which could otherwise repeat the action.",
            PrimaryButtonText = "I verified the outcome", CloseButtonText = "Keep blocked", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.AcknowledgeVerified();
    }
}
