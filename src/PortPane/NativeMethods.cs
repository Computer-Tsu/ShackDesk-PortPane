using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PortPane;

/// <summary>
/// P/Invoke declarations for single-instance window activation.
/// </summary>
internal static class NativeMethods
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    internal static void BringExistingInstanceToForeground()
    {
        string procName = Process.GetCurrentProcess().ProcessName;
        Process? existing = Process
            .GetProcessesByName(procName)
            .FirstOrDefault(p => p.Id != Environment.ProcessId);

        if (existing?.MainWindowHandle is { } hwnd && hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }
}
