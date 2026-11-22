using Windows.Storage.Pickers;
using WinRT.Interop;

namespace shitbotteam.ultra.desktop.Services;

public static class DesktopFilePicker
{
    public static async Task<string?> PickAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        foreach (var extension in extensions) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        return (await picker.PickSingleFileAsync())?.Path;
    }
}
