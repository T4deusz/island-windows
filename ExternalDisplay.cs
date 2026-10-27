using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Island;
internal static class ExternalDisplay
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct PhysicalMonitor { internal IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Description; }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rectangle, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool GetMonitorBrightness(IntPtr monitor, out uint min, out uint current, out uint max);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool SetMonitorBrightness(IntPtr monitor, uint value);
    internal static List<(string Name, int Value)> Read() => Access(null, null);
    internal static void Set(string name, int value) => Access(name, value);
    private static List<(string Name, int Value)> Access(string? target, int? value)
    {
        var result = new List<(string, int)>(); Exception? error = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint count) || count == 0 || count > 32) return true;
            var physical = new PhysicalMonitor[count]; if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical)) return true;
            try
            {
                foreach (var display in physical) if (GetMonitorBrightness(display.Handle, out uint min, out uint current, out uint max) && max > min)
                {
                    string name = display.Description; result.Add((name, (int)((current - min) * 100d / (max - min))));
                    if (name == target && value.HasValue && !SetMonitorBrightness(display.Handle, min + (uint)((max - min) * Math.Clamp(value.Value, 0, 100) / 100d))) error = new System.ComponentModel.Win32Exception();
                }
            }
            finally { DestroyPhysicalMonitors(count, physical); }
            return true;
        }, IntPtr.Zero);
        if (error != null) throw error; if (target != null && !result.Exists(r => r.Item1 == target)) throw new InvalidOperationException("Display is no longer available or does not support DDC/CI."); return result;
    }
}
