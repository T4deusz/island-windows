using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Island;
internal sealed class VolumeKeyHook : IDisposable
{
    internal const int Message = 0x8000 + 71;
    internal static long CleanUntil;
    internal static int CleanSeconds => (int)Math.Max(0, (CleanUntil - Environment.TickCount64 + 999) / 1000);
    internal static void CleanKeyboard() => CleanUntil = Environment.TickCount64 + 20_000;
    private readonly Func<bool> enabled;
    private readonly IntPtr window;
    private readonly HookProc callback;
    private readonly HashSet<int> held = new();
    private IntPtr hook;
    internal bool Installed => hook != IntPtr.Zero;
    internal VolumeKeyHook(IntPtr hwnd, Func<bool> canIntercept)
    {
        window = hwnd; enabled = canIntercept; callback = Handle;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
    }
    private IntPtr Handle(int code, IntPtr message, IntPtr data)
    {
        // Only volume keys are examined. Never log input or run COM work inside a low-level hook.
        if (code >= 0)
        {
            int key = Marshal.ReadInt32(data), flags = Marshal.ReadInt32(data, 8), kind = message.ToInt32();
            if (Environment.TickCount64 < CleanUntil)
            {
                if (key == 0x1B && (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x12) & 0x8000) != 0) CleanUntil = 0;
                else return new IntPtr(1);
            }
            if (key is >= 0xAD and <= 0xAF && (flags & 0x10) == 0)
            {
                if (kind is 0x101 or 0x105 && held.Remove(key)) return new IntPtr(1);
                if (kind is 0x100 or 0x104 && enabled() && PostMessage(window, Message, new IntPtr(key), IntPtr.Zero))
                {
                    held.Add(key); return new IntPtr(1);
                }
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    internal void Reset() => held.Clear();
    public void Dispose() { CleanUntil = 0; if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } held.Clear(); }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
