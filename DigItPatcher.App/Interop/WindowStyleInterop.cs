using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DigItPatcher.App.Interop;

/// <summary>Edits a window's Win32 style bits after its handle exists.</summary>
internal static class WindowStyleInterop
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    /// <summary>Takes the maximize box off a window, which also stops caption double-click and Win+Up.</summary>
    /// <param name="window">The target window.</param>
    public static void DisableMaximize(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_MAXIMIZEBOX);
    }
}
