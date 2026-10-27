using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Island;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Power { public byte AC, Flag, Percent, Reserved; public int Life, FullLife; }
    [DllImport("kernel32.dll")] internal static extern bool GetSystemPowerStatus(out Power status);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("shell32.dll")] internal static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    internal static System.Windows.Point CursorPosition() { GetCursorPos(out var point); return new System.Windows.Point(point.X, point.Y); }
    internal static bool PresentationActive() => SHQueryUserNotificationState(out int state) == 0 && state is 2 or 3 or 4;
    [StructLayout(LayoutKind.Sequential)] internal struct SystemTime { public ushort Year, Month, WeekDay, Day, Hour, Minute, Second, Millisecond; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct BluetoothInfo
    {
        public uint Size; public ulong Address; public uint Class;
        [MarshalAs(UnmanagedType.Bool)] public bool Connected;
        [MarshalAs(UnmanagedType.Bool)] public bool Remembered;
        [MarshalAs(UnmanagedType.Bool)] public bool Authenticated;
        public SystemTime LastSeen, LastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct BluetoothSearch
    {
        public uint Size;
        [MarshalAs(UnmanagedType.Bool)] public bool Authenticated;
        [MarshalAs(UnmanagedType.Bool)] public bool Remembered;
        [MarshalAs(UnmanagedType.Bool)] public bool Unknown;
        [MarshalAs(UnmanagedType.Bool)] public bool Connected;
        [MarshalAs(UnmanagedType.Bool)] public bool Inquiry;
        public byte Timeout; public IntPtr Radio;
    }
    [DllImport("bthprops.cpl", SetLastError = true)] private static extern IntPtr BluetoothFindFirstDevice(ref BluetoothSearch search, ref BluetoothInfo device);
    [DllImport("bthprops.cpl", SetLastError = true)] private static extern bool BluetoothFindNextDevice(IntPtr find, ref BluetoothInfo device);
    [DllImport("bthprops.cpl")] private static extern bool BluetoothFindDeviceClose(IntPtr find);
    internal static List<(string Id, string Name)> ConnectedBluetooth()
    {
        var result = new List<(string, string)>();
        var search = new BluetoothSearch { Size = (uint)Marshal.SizeOf<BluetoothSearch>(), Connected = true };
        var device = new BluetoothInfo { Size = (uint)Marshal.SizeOf<BluetoothInfo>(), Name = "" };
        var find = BluetoothFindFirstDevice(ref search, ref device); if (find == IntPtr.Zero) return result;
        try { do { if (device.Connected) result.Add((device.Address.ToString("X"), device.Name)); device.Size = (uint)Marshal.SizeOf<BluetoothInfo>(); } while (BluetoothFindNextDevice(find, ref device)); }
        finally { BluetoothFindDeviceClose(find); } return result;
    }
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class DeviceEnumerator { }
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceEnumerator
{
    int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IAudioDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioDevice
{
    [PreserveSig] int Activate(ref Guid id, int context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr callback);
    int UnregisterControlChangeNotify(IntPtr callback);
    int GetChannelCount(out uint count);
    int SetMasterVolumeLevel(float level, ref Guid context);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
    int GetMasterVolumeLevel(out float level);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
    int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
    int GetChannelVolumeLevel(uint channel, out float level);
    int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
internal static class Audio
{
    internal static void ToggleMute()
        => ToggleEndpointMute(0, 1);
    internal static void ToggleMicrophone()
        => ToggleEndpointMute(1, 2);
    private static void ToggleEndpointMute(int flow, int role)
    {
        object? enumerator = null, endpoint = null; IAudioDevice? device = null;
        try
        {
            enumerator = new DeviceEnumerator(); Marshal.ThrowExceptionForHR(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(flow, role, out device));
            var id = typeof(IEndpointVolume).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out endpoint));
            var volume = (IEndpointVolume)endpoint; Marshal.ThrowExceptionForHR(volume.GetMute(out bool muted));
            var context = Guid.Empty; Marshal.ThrowExceptionForHR(volume.SetMute(!muted, ref context));
        }
        finally { if (endpoint != null) Marshal.ReleaseComObject(endpoint); if (device != null) Marshal.ReleaseComObject(device); if (enumerator != null) Marshal.ReleaseComObject(enumerator); }
    }
    internal static (int Level, bool Muted)? Read()
    {
        object? enumerator = null; IAudioDevice? device = null; object? endpoint = null;
        try
        {
            enumerator = new DeviceEnumerator();
            Marshal.ThrowExceptionForHR(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device));
            var id = typeof(IEndpointVolume).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out endpoint));
            var volume = (IEndpointVolume)endpoint; Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out float level)); Marshal.ThrowExceptionForHR(volume.GetMute(out bool muted));
            return ((int)Math.Round(level * 100), muted);
        }
        catch (COMException) { return null; }
        finally { if (endpoint != null) Marshal.ReleaseComObject(endpoint); if (device != null) Marshal.ReleaseComObject(device); if (enumerator != null) Marshal.ReleaseComObject(enumerator); }
    }
    internal static void Set(int level)
    {
        object? enumerator = null; IAudioDevice? device = null; object? endpoint = null;
        try
        {
            enumerator = new DeviceEnumerator(); Marshal.ThrowExceptionForHR(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device));
            var id = typeof(IEndpointVolume).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out endpoint));
            var context = Guid.Empty; var volume = (IEndpointVolume)endpoint;
            Marshal.ThrowExceptionForHR(volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0, 100) / 100f, ref context));
            if (level > 0) Marshal.ThrowExceptionForHR(volume.SetMute(false, ref context));
        }
        finally { if (endpoint != null) Marshal.ReleaseComObject(endpoint); if (device != null) Marshal.ReleaseComObject(device); if (enumerator != null) Marshal.ReleaseComObject(enumerator); }
    }
}
