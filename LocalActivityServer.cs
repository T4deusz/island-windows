using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Island;
public sealed class LiveActivity
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "activity";
    public double? Progress { get; set; }
    public long? Tokens { get; set; }
    public decimal? CostUsd { get; set; }
    public string Provider { get; set; } = "";
    public string? Decision { get; set; }
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;
}
internal sealed class LocalActivityServer : IDisposable
{
    internal readonly ConcurrentDictionary<string, LiveActivity> Activities = new();
    private TcpListener? listener;
    private CancellationTokenSource? cancellation;
    private string? token;
    private readonly SemaphoreSlim connections = new(16);
    internal string Status { get; private set; } = "Off · nothing is listening";
    internal int BoundPort => (listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;
    internal string Token => token ??= LoadToken();
    private static string LoadToken()
    {
        string file = Path.Combine(Preferences.Folder, "developer-token.bin"); Directory.CreateDirectory(Preferences.Folder);
        if (File.Exists(file)) return Encoding.UTF8.GetString(SecretStorage.Unprotect(File.ReadAllBytes(file)));
        string value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); File.WriteAllBytes(file, SecretStorage.Protect(Encoding.UTF8.GetBytes(value))); return value;
    }
    internal void Start(int port = 9999)
    {
        if (listener != null) return;
        try { _ = Token; cancellation = new(); listener = new TcpListener(IPAddress.Loopback, port); listener.Start(16); Status = $"Listening on 127.0.0.1:{BoundPort} · authenticated only"; _ = Accept(listener, cancellation.Token); }
        catch (Exception ex) { listener?.Stop(); listener = null; Status = "Could not start · " + ex.Message; }
    }
    private async Task Accept(TcpListener server, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { var client = await server.AcceptTcpClientAsync(stop); if (!connections.Wait(0)) { client.Dispose(); continue; } _ = ServeLimited(client); }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { break; }
        }
    }
    private async Task ServeLimited(TcpClient client) { try { await Serve(client); } catch (Exception ex) { Log.Write(ex); } finally { connections.Release(); } }
    private async Task Serve(TcpClient client)
    {
        using (client)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            var stream = client.GetStream(); var stop = timeout.Token;
            try
            {
                var header = new List<byte>(1024); byte[] single = new byte[1];
                while (header.Count < 8192) { if (await stream.ReadAsync(single, stop) == 0) return; header.Add(single[0]); int n = header.Count; if (n >= 4 && header[n - 4] == 13 && header[n - 3] == 10 && header[n - 2] == 13 && header[n - 1] == 10) break; }
                if (header.Count >= 8192) { await Reply(stream, 413, new { error = "Headers too large" }, stop); return; }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n"); var request = lines[0].Split(' ');
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in lines.Skip(1).Where(l => l.Contains(':'))) { int colon = line.IndexOf(':'); if (!fields.TryAdd(line[..colon], line[(colon + 1)..].Trim())) throw new InvalidDataException("Duplicate header"); }
                string auth = fields.GetValueOrDefault("Authorization", ""); var expected = Encoding.UTF8.GetBytes("Bearer " + Token); var supplied = Encoding.UTF8.GetBytes(auth);
                if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected)) { await Reply(stream, 401, new { error = "Authorization required" }, stop); return; }
                if (request.Length != 3 || request[1] != "/activities" || fields.ContainsKey("Transfer-Encoding")) { await Reply(stream, 400, new { error = "Use /activities without transfer encoding" }, stop); return; }
                if (request[0] == "GET") { await Reply(stream, 200, Activities.Values.OrderByDescending(a => a.Updated).ToArray(), stop); return; }
                if (request[0] != "POST" || !int.TryParse(fields.GetValueOrDefault("Content-Length"), out int length) || length < 2 || length > 65536) { await Reply(stream, 400, new { error = "POST requires a JSON body under 64 KB" }, stop); return; }
                byte[] body = new byte[length]; await stream.ReadExactlyAsync(body, stop);
                var activity = JsonSerializer.Deserialize<LiveActivity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException();
                Validate(activity); activity.Updated = DateTimeOffset.Now;
                if (Activities.TryGetValue(activity.Id, out var existing)) activity.Decision = existing.Decision; else activity.Decision = null;
                if (Activities.Count >= 100 && !Activities.ContainsKey(activity.Id)) { await Reply(stream, 429, new { error = "Activity limit reached" }, stop); return; }
                Activities[activity.Id] = activity;
                await Reply(stream, 200, activity, stop);
            }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or OperationCanceledException) { try { await Reply(stream, 400, new { error = "Invalid or incomplete request" }, stop); } catch { } }
        }
    }
    internal static void Validate(LiveActivity activity)
    {
        if (string.IsNullOrWhiteSpace(activity.Id) || activity.Id.Length > 100 || string.IsNullOrWhiteSpace(activity.Title) || activity.Title.Length > 300 || activity.Provider == null || activity.Provider.Length > 50 || activity.Kind is not ("activity" or "build" or "download" or "agent" or "approval" or "usage" or "complete" or "error") || activity.Progress.HasValue && (!double.IsFinite(activity.Progress.Value) || activity.Progress < 0 || activity.Progress > 1) || activity.Tokens < 0 || activity.CostUsd < 0) throw new ArgumentException("Invalid activity");
    }
    private static async Task Reply(NetworkStream stream, int code, object payload, CancellationToken stop)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload); byte[] head = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} Result\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, stop); await stream.WriteAsync(body, stop);
    }
    internal void Stop() { cancellation?.Cancel(); listener?.Stop(); listener = null; cancellation?.Dispose(); cancellation = null; Status = "Off · nothing is listening"; }
    public void Dispose() => Stop();
}
internal static class SecretStorage
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Blob { internal int Length; internal IntPtr Data; }
    [System.Runtime.InteropServices.DllImport("crypt32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [System.Runtime.InteropServices.DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    internal static byte[] Protect(byte[] data) => Convert(data, true);
    internal static byte[] Unprotect(byte[] data) => Convert(data, false);
    private static byte[] Convert(byte[] data, bool protect)
    {
        var input = new Blob { Length = data.Length, Data = System.Runtime.InteropServices.Marshal.AllocHGlobal(data.Length) }; var output = new Blob();
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, 0, input.Data, data.Length);
            bool ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new System.ComponentModel.Win32Exception(); byte[] result = new byte[output.Length]; System.Runtime.InteropServices.Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
}
