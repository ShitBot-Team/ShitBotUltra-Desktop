using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace shitbotteam.ultra.desktop.Services;

/// <summary>
/// Owns the notification-area icon and the window subclass used to intercept
/// close requests. The tray never owns or stops the Core process.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const uint WmClose = 0x0010;
    private const uint WmApp = 0x8000;
    private const uint TrayCallbackMessage = WmApp + 0x31;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint MfString = 0x00000000;
    private const uint SwHide = 0;
    private const uint SwShow = 5;
    private const uint SwRestore = 9;
    private const uint IdOpen = 1001;
    private const uint IdExit = 1002;
    private static readonly UIntPtr SubclassId = new(0x53425531);
    private static readonly IntPtr ApplicationIconId = new(32512);

    private readonly IntPtr _windowHandle;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SubclassProcedure _subclassProcedure;
    private readonly uint _taskbarCreatedMessage;
    private bool _iconAdded;
    private bool _allowClose;
    private bool _disposed;
    private bool _minimizeOnClose;

    public event Action? ExitRequested;

    public bool MinimizeOnClose
    {
        get => _minimizeOnClose;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_minimizeOnClose == value)
            {
                return;
            }

            if (value)
            {
                AddIcon();
                _minimizeOnClose = true;
            }
            else
            {
                _minimizeOnClose = false;
                RemoveIcon();
            }
        }
    }

    public TrayIconService(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windowHandle = WindowNative.GetWindowHandle(window);
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The WinUI dispatcher is unavailable.");
        _subclassProcedure = WindowSubclassProcedure;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

        if (_windowHandle == IntPtr.Zero
            || !SetWindowSubclass(_windowHandle, _subclassProcedure, SubclassId, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install the notification-area window hook.");
        }
    }

    public void AllowClose() => _allowClose = true;

    private void AddIcon()
    {
        if (_iconAdded)
        {
            return;
        }

        var data = CreateIconData();
        if (!Shell_NotifyIcon(NimAdd, ref data))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not create the notification-area icon.");
        }

        _iconAdded = true;
    }

    private void RemoveIcon()
    {
        if (!_iconAdded)
        {
            return;
        }

        var data = CreateIconData();
        Shell_NotifyIcon(NimDelete, ref data);
        _iconAdded = false;
    }

    private NotifyIconData CreateIconData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _windowHandle,
        Id = 1,
        Flags = NifMessage | NifIcon | NifTip,
        CallbackMessage = TrayCallbackMessage,
        IconHandle = LoadIcon(IntPtr.Zero, ApplicationIconId),
        Tip = "ShitBotUltra Desktop",
        Info = string.Empty,
        InfoTitle = string.Empty
    };

    private IntPtr WindowSubclassProcedure(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr referenceData)
    {
        if (_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage && _minimizeOnClose)
        {
            // Explorer discards notification icons after restarting. Never hide
            // the only window if the icon cannot be restored.
            _iconAdded = false;
            try { AddIcon(); }
            catch { _minimizeOnClose = false; RestoreWindow(); }
        }

        if (message == WmClose && MinimizeOnClose && !_allowClose)
        {
            ShowWindow(windowHandle, SwHide);
            return IntPtr.Zero;
        }

        if (message == TrayCallbackMessage && _iconAdded)
        {
            var notification = unchecked((uint)(lParam.ToInt64() & 0xFFFF));
            switch (notification)
            {
                case WmLButtonUp:
                case WmLButtonDoubleClick:
                    RestoreWindow();
                    return IntPtr.Zero;
                case WmRButtonUp:
                    ShowContextMenu();
                    return IntPtr.Zero;
            }
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void RestoreWindow()
    {
        ShowWindow(_windowHandle, SwShow);
        ShowWindow(_windowHandle, SwRestore);
        SetForegroundWindow(_windowHandle);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MfString, IdOpen, "Open ShitBotUltra Desktop");
            AppendMenu(menu, MfString, IdExit, "Exit");
            GetCursorPos(out var point);
            SetForegroundWindow(_windowHandle);
            var selected = TrackPopupMenu(
                menu,
                TpmRightButton | TpmReturnCommand,
                point.X,
                point.Y,
                0,
                _windowHandle,
                IntPtr.Zero);
            PostMessage(_windowHandle, 0, IntPtr.Zero, IntPtr.Zero);

            if (selected == IdOpen)
            {
                _dispatcherQueue.TryEnqueue(RestoreWindow);
            }
            else if (selected == IdExit)
            {
                _dispatcherQueue.TryEnqueue(() => ExitRequested?.Invoke());
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RemoveIcon();
        RemoveWindowSubclass(_windowHandle, _subclassProcedure, SubclassId);
        ExitRequested = null;
    }

    private delegate IntPtr SubclassProcedure(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr referenceData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr windowHandle,
        SubclassProcedure callback,
        UIntPtr subclassId,
        IntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr windowHandle,
        SubclassProcedure callback,
        UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr windowHandle, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, uint command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint itemId, string itemText);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenu(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        int reserved,
        IntPtr windowHandle,
        IntPtr rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
