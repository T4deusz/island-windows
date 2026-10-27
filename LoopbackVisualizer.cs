using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Island;
// Audio never leaves this worker's in-memory buffers. No microphone or recording files are used.
internal sealed class LoopbackVisualizer : IDisposable
{
    private readonly object gate = new();
    private readonly float[] levels = new float[8];
    private Thread? thread;
    private volatile bool enabled, disposed;
    private string status = "Loopback idle";
    internal string Status => Volatile.Read(ref status);
    internal long FramesCaptured;
    internal float Level(int band) => Volatile.Read(ref levels[band]);
    internal void SetEnabled(bool value)
    {
        lock (gate)
        {
            enabled = value && !disposed;
            if (enabled && thread == null) { thread = new Thread(Run) { IsBackground = true, Name = "Island WASAPI loopback" }; thread.SetApartmentState(ApartmentState.MTA); thread.Start(); }
            Monitor.PulseAll(gate);
        }
    }
    private void Run()
    {
        while (!disposed)
        {
            lock (gate) { while (!enabled && !disposed) Monitor.Wait(gate); }
            if (disposed) break;
            try { Capture(); }
            catch (Exception ex) { Volatile.Write(ref status, "Loopback unavailable · " + ex.GetType().Name); Log.Write(ex); lock (gate) { if (enabled && !disposed) Monitor.Wait(gate, 2000); } }
        }
    }
    private void Capture()
    {
        object? enumerator = null, clientObject = null, captureObject = null;
        IAudioDevice? device = null; IntPtr format = IntPtr.Zero;
        IAudioClient? client = null;
        try
        {
            enumerator = new DeviceEnumerator(); Marshal.ThrowExceptionForHR(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device));
            var clientId = typeof(IAudioClient).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref clientId, 23, IntPtr.Zero, out clientObject)); client = (IAudioClient)clientObject;
            Marshal.ThrowExceptionForHR(client.GetMixFormat(out format));
            int tag = (ushort)Marshal.ReadInt16(format, 0), channels = (ushort)Marshal.ReadInt16(format, 2), rate = Marshal.ReadInt32(format, 4), align = (ushort)Marshal.ReadInt16(format, 12), bits = (ushort)Marshal.ReadInt16(format, 14);
            if (tag == 0xFFFE) tag = Marshal.ReadInt32(format, 24);
            bool floating = tag == 3 && bits == 32;
            if (channels < 1 || channels > 32 || rate < 8000 || align != channels * (bits / 8) || !(floating || tag == 1 && bits is 16 or 24 or 32)) throw new NotSupportedException("Unsupported output audio format");
            Marshal.ThrowExceptionForHR(client.Initialize(0, 0x20000, 1_000_000, 0, format, IntPtr.Zero));
            var captureId = typeof(IAudioCaptureClient).GUID; Marshal.ThrowExceptionForHR(client.GetService(ref captureId, out captureObject)); var capture = (IAudioCaptureClient)captureObject;
            Marshal.ThrowExceptionForHR(client.Start()); Volatile.Write(ref status, $"WASAPI · {rate / 1000d:0.#} kHz · {channels} ch");
            byte[] bytes = new byte[rate * align / 5]; var analyzer = new Spectrum(rate); int bytesPerSample = bits / 8;
            long lastPacket = Environment.TickCount64, lastEndpointCheck = lastPacket;
            device.GetId(out string endpointId);
            while (enabled && !disposed)
            {
                Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out uint packet));
                while (packet > 0)
                {
                    Marshal.ThrowExceptionForHR(capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _));
                    try
                    {
                        int length = checked((int)frames * align); if (bytes.Length < length) bytes = new byte[length];
                        bool silence = (flags & 2) != 0; if (!silence) Marshal.Copy(data, bytes, 0, length);
                        if ((flags & 1) != 0) analyzer.Reset();
                        for (int frame = 0; frame < frames; frame++)
                        {
                            double mono = 0;
                            if (!silence) for (int channel = 0; channel < channels; channel++) mono += Decode(bytes, frame * align + channel * bytesPerSample, bits, floating);
                            if (analyzer.Push(mono / channels)) for (int i = 0; i < 8; i++) Volatile.Write(ref levels[i], (float)analyzer.Bands[i]);
                        }
                        Interlocked.Add(ref FramesCaptured, frames); lastPacket = Environment.TickCount64;
                    }
                    finally { Marshal.ThrowExceptionForHR(capture.ReleaseBuffer(frames)); }
                    Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out packet));
                }
                if (Environment.TickCount64 - lastPacket > 150) for (int i = 0; i < 8; i++) Volatile.Write(ref levels[i], 0);
                if (Environment.TickCount64 - lastEndpointCheck > 1500)
                {
                    lastEndpointCheck = Environment.TickCount64;
                    if (((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out var current) >= 0)
                    {
                        try { current.GetId(out string currentId); if (currentId != endpointId) return; } finally { Marshal.ReleaseComObject(current); }
                    }
                }
                lock (gate) { if (enabled && !disposed) Monitor.Wait(gate, 15); }
            }
        }
        finally
        {
            client?.Stop(); if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
            if (captureObject != null) Marshal.ReleaseComObject(captureObject); if (clientObject != null) Marshal.ReleaseComObject(clientObject);
            if (device != null) Marshal.ReleaseComObject(device); if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            for (int i = 0; i < 8; i++) Volatile.Write(ref levels[i], 0);
            if (!enabled) Volatile.Write(ref status, "Loopback idle");
        }
    }
    internal static double Decode(byte[] data, int offset, int bits, bool floating) => floating ? Math.Clamp(float.IsFinite(BitConverter.ToSingle(data, offset)) ? BitConverter.ToSingle(data, offset) : 0, -1, 1) : bits switch
    {
        16 => BitConverter.ToInt16(data, offset) / 32768d,
        24 => ((data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16) << 8 >> 8) / 8388608d,
        32 => BitConverter.ToInt32(data, offset) / 2147483648d,
        _ => 0
    };
    public void Dispose() { lock (gate) { disposed = true; enabled = false; Monitor.PulseAll(gate); } thread?.Join(500); }
}
internal sealed class Spectrum
{
    private const int Size = 1024;
    private readonly double[] samples = new double[Size], real = new double[Size], imaginary = new double[Size];
    private readonly int rate;
    private int count;
    internal readonly double[] Bands = new double[8];
    private static readonly double[] edges = { 30, 100, 220, 450, 900, 1800, 3500, 7000, 20000 };
    internal Spectrum(int sampleRate) { rate = sampleRate; }
    internal void Reset() { count = 0; Array.Clear(samples); }
    internal bool Push(double value)
    {
        samples[count++] = double.IsFinite(value) ? value : 0; if (count < Size) return false; count = 0;
        for (int i = 0; i < Size; i++) { real[i] = samples[i] * (.5 - .5 * Math.Cos(2 * Math.PI * i / (Size - 1))); imaginary[i] = 0; }
        for (int i = 1, j = 0; i < Size; i++)
        {
            int bit = Size >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
            if (i < j) { (real[i], real[j]) = (real[j], real[i]); }
        }
        for (int length = 2; length <= Size; length <<= 1)
        {
            double angle = -2 * Math.PI / length, wr = Math.Cos(angle), wi = Math.Sin(angle);
            for (int start = 0; start < Size; start += length)
            {
                double ur = 1, ui = 0;
                for (int j = 0; j < length / 2; j++)
                {
                    int a = start + j, b = a + length / 2; double vr = real[b] * ur - imaginary[b] * ui, vi = real[b] * ui + imaginary[b] * ur;
                    real[b] = real[a] - vr; imaginary[b] = imaginary[a] - vi; real[a] += vr; imaginary[a] += vi;
                    (ur, ui) = (ur * wr - ui * wi, ur * wi + ui * wr);
                }
            }
        }
        Array.Clear(Bands);
        for (int i = 1; i < Size / 2; i++)
        {
            double frequency = i * rate / (double)Size;
            for (int band = 0; band < 8; band++) if (frequency >= edges[band] && frequency < edges[band + 1]) { Bands[band] += real[i] * real[i] + imaginary[i] * imaginary[i]; break; }
        }
        for (int band = 0; band < 8; band++) Bands[band] = Math.Clamp((20 * Math.Log10(Math.Max(1e-8, Math.Sqrt(Bands[band]) / (Size * .3))) + 60) / 60, 0, 1);
        return true;
    }
}
[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig] int Initialize(int mode, uint flags, long duration, long periodicity, IntPtr format, IntPtr session);
    [PreserveSig] int GetBufferSize(out uint size);
    [PreserveSig] int GetStreamLatency(out long latency);
    [PreserveSig] int GetCurrentPadding(out uint padding);
    [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
    [PreserveSig] int GetMixFormat(out IntPtr format);
    [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr handle);
    [PreserveSig] int GetService(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}
[ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioCaptureClient
{
    [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong timestamp);
    [PreserveSig] int ReleaseBuffer(uint frames);
    [PreserveSig] int GetNextPacketSize(out uint frames);
}
