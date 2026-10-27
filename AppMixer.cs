using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Island;
internal sealed class AppMixer : IDisposable
{
    private object? control;
    internal string Name { get; }
    internal float Volume { get; }
    internal bool Muted { get; }
    private AppMixer(object session)
    {
        control = session; var volume = (ISimpleVolume)session; Marshal.ThrowExceptionForHR(volume.GetMasterVolume(out float level)); Marshal.ThrowExceptionForHR(volume.GetMute(out bool muted)); Volume = level; Muted = muted;
        var info = (ISessionControl2)session; info.GetDisplayName(out string display); info.GetProcessId(out uint process);
        try { Name = process == 0 ? "System sounds" : Process.GetProcessById((int)process).ProcessName; } catch { Name = string.IsNullOrEmpty(display) ? "Audio session" : display; }
    }
    internal void SetVolume(float value) { if (control == null) return; var context = Guid.Empty; Marshal.ThrowExceptionForHR(((ISimpleVolume)control).SetMasterVolume(Math.Clamp(value, 0, 1), ref context)); }
    internal void SetMute(bool value) { if (control == null) return; var context = Guid.Empty; Marshal.ThrowExceptionForHR(((ISimpleVolume)control).SetMute(value, ref context)); }
    internal static List<AppMixer> Enumerate()
    {
        var result = new List<AppMixer>(); object? enumerator = null, manager = null; IAudioDevice? device = null; ISessionEnumerator? sessions = null;
        try
        {
            enumerator = new DeviceEnumerator(); Marshal.ThrowExceptionForHR(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device)); var id = typeof(ISessionManager2).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out manager));
            Marshal.ThrowExceptionForHR(((ISessionManager2)manager).GetSessionEnumerator(out sessions)); Marshal.ThrowExceptionForHR(sessions.GetCount(out int count));
            for (int i = 0; i < count; i++) { Marshal.ThrowExceptionForHR(sessions.GetSession(i, out object control)); try { result.Add(new AppMixer(control)); } catch { Marshal.ReleaseComObject(control); } }
            return result;
        }
        catch { foreach (var item in result) item.Dispose(); throw; }
        finally { if (sessions != null) Marshal.ReleaseComObject(sessions); if (manager != null) Marshal.ReleaseComObject(manager); if (device != null) Marshal.ReleaseComObject(device); if (enumerator != null) Marshal.ReleaseComObject(enumerator); }
    }
    public void Dispose() { if (control != null) { Marshal.ReleaseComObject(control); control = null; } }
}
[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISessionManager2
{
    [PreserveSig] int GetAudioSessionControl(IntPtr session, uint flags, out IntPtr control);
    [PreserveSig] int GetSimpleAudioVolume(IntPtr session, uint flags, out IntPtr volume);
    [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
}
[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISessionEnumerator
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
}
[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISessionControl2
{
    [PreserveSig] int GetState(out int state);
    [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
    [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
    [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
    [PreserveSig] int GetGroupingParam(out Guid grouping);
    [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
    [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
    [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
    [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetProcessId(out uint process);
}
[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleVolume
{
    [PreserveSig] int SetMasterVolume(float level, ref Guid context);
    [PreserveSig] int GetMasterVolume(out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
}
