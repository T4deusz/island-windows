using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Island;
internal static class DesktopNative
{
    [DllImport("user32.dll")] internal static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool RemoveClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("kernel32.dll")] internal static extern uint SetThreadExecutionState(uint flags);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [StructLayout(LayoutKind.Sequential)] private struct Memory { internal uint Length, Load; internal ulong Total, Available, Page, AvailablePage, Virtual, AvailableVirtual, Extended; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref Memory status);
    private static long idleBefore, totalBefore;
    internal static string SystemStats()
    {
        GetSystemTimes(out long idle, out long kernel, out long user); long total = kernel + user, elapsed = total - totalBefore;
        double cpu = totalBefore == 0 || elapsed <= 0 ? 0 : (1 - (idle - idleBefore) / (double)elapsed) * 100; idleBefore = idle; totalBefore = total;
        var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() }; GlobalMemoryStatusEx(ref memory);
        return $"CPU {Math.Clamp(cpu, 0, 100):0}%     Memory {(memory.Total - memory.Available) / 1073741824d:0.0} / {memory.Total / 1073741824d:0.0} GB";
    }
    internal static void SnapWindow(IntPtr window, string zone)
    {
        if (window == IntPtr.Zero || !IsWindow(window)) throw new InvalidOperationException("Choose another window before opening Island.");
        var bounds = System.Windows.Forms.Screen.FromHandle(window).WorkingArea; ShowWindow(window, 9);
        if (zone == "Maximize") { ShowWindow(window, 3); return; }
        int x = bounds.Left, y = bounds.Top, width = bounds.Width, height = bounds.Height;
        if (zone is "Left" or "Right") { width /= 2; if (zone == "Right") x += width; }
        if (zone is "Top" or "Bottom") { height /= 2; if (zone == "Bottom") y += height; }
        Native.SetWindowPos(window, IntPtr.Zero, x, y, width, height, 0x0014);
    }
    internal static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    internal static void ApplyCapturePrivacy(Window window) => SetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, App.Prefs.HideFromCapture ? 0x11u : 0u);
}
