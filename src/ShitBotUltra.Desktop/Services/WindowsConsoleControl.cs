using System.Runtime.InteropServices;

namespace shitbotteam.ultra.desktop.Services;

internal static class WindowsConsoleControl
{
    private const uint CtrlCEvent = 0;
    private const int HideWindow = 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    public static void TryHideConsole(uint processId)
    {
        if (!AttachConsole(processId))
        {
            return;
        }

        try
        {
            var window = GetConsoleWindow();
            if (window != IntPtr.Zero)
            {
                ShowWindow(window, HideWindow);
            }
        }
        finally
        {
            FreeConsole();
        }
    }

    public static bool TrySendInterrupt(uint processId)
    {
        if (!AttachConsole(processId))
        {
            return false;
        }

        try
        {
            SetConsoleCtrlHandler(IntPtr.Zero, add: true);
            return GenerateConsoleCtrlEvent(CtrlCEvent, 0);
        }
        finally
        {
            FreeConsole();
            SetConsoleCtrlHandler(IntPtr.Zero, add: false);
        }
    }
}
