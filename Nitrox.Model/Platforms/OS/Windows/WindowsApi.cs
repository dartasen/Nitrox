using System;
using System.Runtime.InteropServices;
using static Nitrox.Model.Platforms.OS.Windows.Internal.Win32Native;

namespace Nitrox.Model.Platforms.OS.Windows;

public static partial class WindowsApi
{
    /// <summary>
    ///     Applies default OS animations to the window handle.
    /// </summary>
    /// <remarks>
    ///     Note on Windows OS: it will force enable resizing of a Window if <see cref="canResize" /> is true. Make sure to set
    ///     it correctly.
    /// </remarks>
    public static void EnableDefaultWindowAnimations(nint windowHandle, bool canResize)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        WS dwNewLong = WS.WS_CAPTION | WS.WS_CLIPCHILDREN | WS.WS_MINIMIZEBOX | WS.WS_MAXIMIZEBOX | WS.WS_SYSMENU;
        if (canResize)
        {
            dwNewLong |= WS.WS_SIZEBOX;
        }

        switch (IntPtr.Size)
        {
            case 8:
                SetWindowLongPtr64(windowHandle, -16, (long)dwNewLong);
                break;
            default:
                SetWindowLong32(windowHandle, -16, (int)dwNewLong);
                break;
        }
    }

    public static void BringProcessToFront(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }
        const int SW_RESTORE = 9;
        if (IsIconic(windowHandle))
        {
            ShowWindow(windowHandle, SW_RESTORE);
        }

        SetForegroundWindow(windowHandle);
    }

    /// <summary>
    /// Brings the specified window to the foreground and activates it.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow</remarks>
    /// <param name="handle">The window handle to activate.</param>
#if NET
    [LibraryImport("User32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr handle);
#else
    [DllImport("User32.dll", EntryPoint = "SetForegroundWindow")]
    private static extern bool SetForegroundWindow(IntPtr handle);
#endif

    /// <summary>
    /// Changes how the specified window is shown, hidden, minimized, or restored.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow</remarks>
    /// <param name="handle">The window handle whose show state is being changed.</param>
    /// <param name="nCmdShow">The show command that controls the new window state.</param>
#if NET
    [LibraryImport("User32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr handle, int nCmdShow);
#else
    [DllImport("User32.dll", EntryPoint = "ShowWindow")]
    private static extern bool ShowWindow(IntPtr handle, int nCmdShow);
#endif

    /// <summary>
    /// Checks whether the specified window is minimized.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-isiconic</remarks>
    /// <param name="handle">The window handle to test.</param>
#if NET
    [LibraryImport("User32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr handle);
#else
    [DllImport("User32.dll", EntryPoint = "IsIconic")]
    private static extern bool IsIconic(IntPtr handle);
#endif
}
