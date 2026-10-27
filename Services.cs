using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Devices.Enumeration;
using Windows.Media.Control;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Island;
internal sealed class DesktopServices : IDisposable
{
    internal GlobalSystemMediaTransportControlsSessionManager? Media;
    internal string MediaError = "Starting media service…";
    internal string DeviceSummary = "No connected Bluetooth devices";
    internal string WeatherSummary = "Weather is off";
    internal string NotificationStatus = "Off · enable to request Windows permission";
    internal string BrightnessStatus = "Checking display support…";
    internal int? Brightness;
    private HashSet<string>? devices;
    private HashSet<uint>? notifications;
    private DateTimeOffset notificationsSince;
    private string artworkKey = "";
    private BitmapImage? artwork;
    internal double PositionSeconds, DurationSeconds;
    internal bool CanSeek;
    internal string MediaKey => artworkKey;
    internal event Action? PlaybackChanged;
    internal bool IsPlaying { get; private set; }
    internal bool CanPlay, CanPause, CanToggle, CanPrevious, CanNext, CanShuffle, CanRepeat, CanSpeed;
    internal bool? Shuffle;
    internal Windows.Media.MediaPlaybackAutoRepeatMode? Repeat;
    internal string TrackName = "", ArtistName = "";
    private GlobalSystemMediaTransportControlsSession? trackedSession;
    private bool? pendingPlaying;
    private DateTimeOffset pendingUntil;
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(12) };
    internal async Task Initialize()
    {
        try { Media = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); Media.CurrentSessionChanged += CurrentSessionChanged; AttachSession(); MediaError = "Play something in a media app to begin"; }
        catch (Exception ex) { MediaError = "Windows media service unavailable"; Log.Write(ex); }
    }
    private void CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => AttachSession();
    private void SessionPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => RefreshPlayback();
    private void AttachSession()
    {
        pendingPlaying = null;
        if (trackedSession != null) trackedSession.PlaybackInfoChanged -= SessionPlaybackChanged;
        trackedSession = Media?.GetCurrentSession();
        if (trackedSession != null) trackedSession.PlaybackInfoChanged += SessionPlaybackChanged;
        RefreshPlayback();
    }
    internal void RefreshPlayback()
    {
        try
        {
            var info = (trackedSession ?? Media?.GetCurrentSession())?.GetPlaybackInfo();
            bool actual = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (pendingPlaying.HasValue && (actual == pendingPlaying.Value || DateTimeOffset.Now >= pendingUntil)) pendingPlaying = null;
            IsPlaying = pendingPlaying ?? actual;
            CanPlay = info?.Controls.IsPlayEnabled == true; CanPause = info?.Controls.IsPauseEnabled == true;
            CanToggle = info?.Controls.IsPlayPauseToggleEnabled == true;
            CanPrevious = info?.Controls.IsPreviousEnabled == true; CanNext = info?.Controls.IsNextEnabled == true;
            CanShuffle = info?.Controls.IsShuffleEnabled == true; CanRepeat = info?.Controls.IsRepeatEnabled == true; CanSpeed = info?.Controls.IsPlaybackRateEnabled == true;
            Shuffle = info?.IsShuffleActive; Repeat = info?.AutoRepeatMode;
            PlaybackChanged?.Invoke();
        }
        catch (Exception ex) { Log.Write(ex); }
    }
    internal async Task<(string Title, string Artist, bool Playing, double Progress, BitmapImage? Art)> ReadMedia()
    {
        var session = Media?.GetCurrentSession();
        RefreshPlayback();
        if (session == null) { PositionSeconds = DurationSeconds = 0; CanSeek = false; artworkKey = ""; return ("Nothing playing", MediaError, false, 0, null); }
        var props = await session.TryGetMediaPropertiesAsync();
        TrackName = props.Title; ArtistName = props.Artist;
        var info = session.GetPlaybackInfo(); var timeline = session.GetTimelineProperties();
        var duration = (timeline.EndTime - timeline.StartTime).TotalSeconds;
        var position = (timeline.Position - timeline.StartTime).TotalSeconds;
        if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) position += (DateTimeOffset.Now - timeline.LastUpdatedTime).TotalSeconds * (info.PlaybackRate ?? 1);
        PositionSeconds = Math.Clamp(position, 0, Math.Max(0, duration)); DurationSeconds = Math.Max(0, duration); CanSeek = info.Controls.IsPlaybackPositionEnabled && duration > 0;
        var key = session.SourceAppUserModelId + "|" + props.Title + "|" + props.Artist + "|" + props.AlbumTitle;
        if (key != artworkKey && props.Thumbnail != null)
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            using var input = stream.AsStreamForRead();
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 128; image.StreamSource = input; image.EndInit(); image.Freeze(); artwork = image;
        }
        else if (key != artworkKey) artwork = null;
        artworkKey = key;
        return (string.IsNullOrWhiteSpace(props.Title) ? "Untitled media" : props.Title, props.Artist, info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, duration <= 0 ? 0 : Math.Clamp(position / duration, 0, 1), artwork);
    }
    internal async Task Seek(double fraction, double duration, string expectedKey)
    {
        if (!double.IsFinite(fraction) || !double.IsFinite(duration) || duration <= 0 || expectedKey != artworkKey) return;
        var session = Media?.GetCurrentSession(); if (session == null) return;
        if (!session.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled) throw new InvalidOperationException("This app does not allow seeking.");
        var properties = await session.TryGetMediaPropertiesAsync();
        if (session.SourceAppUserModelId + "|" + properties.Title + "|" + properties.Artist + "|" + properties.AlbumTitle != expectedKey) return;
        var timeline = session.GetTimelineProperties();
        long target = timeline.StartTime.Ticks + (long)((timeline.EndTime.Ticks - timeline.StartTime.Ticks) * Math.Clamp(fraction, 0, 1));
        if (!await session.TryChangePlaybackPositionAsync(target)) throw new InvalidOperationException("The media app rejected the playback position.");
    }
    internal async Task Control(string action)
    {
        var session = Media?.GetCurrentSession(); if (session == null) return;
        RefreshPlayback();
        bool wasPlaying = IsPlaying;
        bool ok = action switch
        {
            "previous" => await session.TrySkipPreviousAsync(), "next" => await session.TrySkipNextAsync(),
            "shuffle" => await session.TryChangeShuffleActiveAsync(!(Shuffle ?? false)),
            "repeat" => await session.TryChangeAutoRepeatModeAsync(Repeat == Windows.Media.MediaPlaybackAutoRepeatMode.Track ? Windows.Media.MediaPlaybackAutoRepeatMode.None : Windows.Media.MediaPlaybackAutoRepeatMode.Track),
            "speed" => await session.TryChangePlaybackRateAsync((session.GetPlaybackInfo().PlaybackRate ?? 1) >= 2 ? .5 : (session.GetPlaybackInfo().PlaybackRate ?? 1) + .25),
            _ => IsPlaying && CanPause ? await session.TryPauseAsync() : !IsPlaying && CanPlay ? await session.TryPlayAsync() : await session.TryTogglePlayPauseAsync()
        };
        if (!ok) throw new InvalidOperationException("This media app does not support that control.");
        if (action == "play") { pendingPlaying = !wasPlaying; pendingUntil = DateTimeOffset.Now.AddSeconds(2); }
        RefreshPlayback();
    }
    internal void ReadBrightness()
    {
        object? locator = null, service = null, rows = null;
        try
        {
            locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator")!);
            service = ((dynamic)locator!).ConnectServer(".", "root\\WMI"); rows = ((dynamic)service).ExecQuery("SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True");
            int? result = null;
            foreach (object row in (System.Collections.IEnumerable)rows) { try { result = (int)((dynamic)row).CurrentBrightness; } finally { Marshal.ReleaseComObject(row); } }
            Brightness = result; BrightnessStatus = result.HasValue ? "Built-in display connected" : "No WMI brightness control · use your monitor controls";
        }
        catch (Exception ex) { Brightness = null; BrightnessStatus = "Brightness unavailable on this display"; Log.Write(ex); }
        finally { if (rows != null) Marshal.ReleaseComObject(rows); if (service != null) Marshal.ReleaseComObject(service); if (locator != null) Marshal.ReleaseComObject(locator); }
    }
    internal void SetBrightness(int value)
    {
        object? locator = null, service = null, rows = null;
        try
        {
            locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator")!); service = ((dynamic)locator!).ConnectServer(".", "root\\WMI");
            rows = ((dynamic)service).ExecQuery("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True");
            foreach (object row in (System.Collections.IEnumerable)rows) { try { uint result = ((dynamic)row).WmiSetBrightness(0u, (byte)Math.Clamp(value, 0, 100)); if (result != 0) throw new InvalidOperationException("The display rejected the brightness change."); } finally { Marshal.ReleaseComObject(row); } }
        }
        finally { if (rows != null) Marshal.ReleaseComObject(rows); if (service != null) Marshal.ReleaseComObject(service); if (locator != null) Marshal.ReleaseComObject(locator); }
    }
    internal async Task<string?> ReadDevices()
    {
        const string connected = "System.Devices.Aep.IsConnected";
        const string battery = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
        DeviceInformationCollection found;
        try { found = await DeviceInformation.FindAllAsync("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")", new[] { connected, battery }, DeviceInformationKind.AssociationEndpoint); }
        catch (UnauthorizedAccessException)
        {
            // shortcut: classic Bluetooth fallback lacks LE battery data; use the packaged edition for AEP access.
            var classic = await Task.Run(Native.ConnectedBluetooth);
            DeviceSummary = classic.Count == 0 ? "No connected Bluetooth devices" : string.Join("\n", classic.Select(d => d.Name + " · connected"));
            var addedClassic = devices == null ? null : classic.Where(d => !devices.Contains(d.Id)).Select(d => d.Name).FirstOrDefault(); devices = classic.Select(d => d.Id).ToHashSet(); return addedClassic;
        }
        var active = found.Where(d => d.Properties.TryGetValue(connected, out var c) && c is true).ToList();
        DeviceSummary = active.Count == 0 ? "No connected Bluetooth devices" : string.Join("\n", active.Select(d => d.Name + (d.Properties.TryGetValue(battery, out var b) && b != null ? $" · {b}%" : " · connected")));
        var next = active.Select(d => d.Id).ToHashSet(); var added = devices == null ? null : active.FirstOrDefault(d => !devices.Contains(d.Id)); devices = next; return added?.Name;
    }
    internal async Task EnableNotifications()
    {
        try
        {
            var access = await UserNotificationListener.Current.RequestAccessAsync();
            App.Prefs.Notifications = access == UserNotificationListenerAccessStatus.Allowed;
            NotificationStatus = App.Prefs.Notifications ? "Connected · new notifications only" : "Access denied · check Windows privacy settings";
            notifications = null; notificationsSince = DateTimeOffset.Now;
        }
        catch (Exception ex) { App.Prefs.Notifications = false; NotificationStatus = "Requires the packaged MSIX edition and Windows permission"; Log.Write(ex); }
        App.Prefs.Save();
    }
    internal async Task<(string Title, string Body)?> ReadNotifications()
    {
        if (!App.Prefs.Notifications) return null;
        try
        {
            var listener = UserNotificationListener.Current;
            if (listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed) { NotificationStatus = "Permission missing · reconnect in Settings"; return null; }
            NotificationStatus = "Connected · new notifications only";
            var list = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            var latest = notifications == null ? null : list.Where(n => !notifications.Contains(n.Id) && n.CreationTime >= notificationsSince).OrderByDescending(n => n.CreationTime).FirstOrDefault();
            notifications = list.Select(n => n.Id).ToHashSet();
            if (latest == null) return null;
            var text = latest.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements();
            return (latest.AppInfo.DisplayInfo.DisplayName, text == null ? "New notification" : string.Join(" · ", text.Select(t => t.Text)));
        }
        catch (Exception ex) { NotificationStatus = "Listener unavailable · reconnect in Settings"; Log.Write(ex); return null; }
    }
    internal async Task ReadWeather()
    {
        if (!App.Prefs.Weather) { WeatherSummary = "Weather is off"; return; }
        try
        {
            using var geo = JsonDocument.Parse(await http.GetStringAsync($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(App.Prefs.City)}&count=1&language=en&format=json"));
            if (!geo.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) { WeatherSummary = "City not found · update Settings"; return; }
            var city = results[0]; string lat = city.GetProperty("latitude").GetDouble().ToString(CultureInfo.InvariantCulture), lon = city.GetProperty("longitude").GetDouble().ToString(CultureInfo.InvariantCulture);
            using var data = JsonDocument.Parse(await http.GetStringAsync($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&timezone=auto"));
            var now = data.RootElement.GetProperty("current"); int code = now.GetProperty("weather_code").GetInt32();
            WeatherSummary = $"{now.GetProperty("temperature_2m").GetDouble():0}°  {WeatherName(code)}\n{city.GetProperty("name").GetString()} · updated {DateTime.Now:t}";
        }
        catch (Exception ex) { WeatherSummary = "Weather unavailable · check your connection"; Log.Write(ex); }
    }
    internal static string WeatherName(int code) => code switch { 0 => "Clear", <= 3 => "Cloudy", 45 or 48 => "Fog", >= 51 and <= 67 => "Rain", >= 71 and <= 77 => "Snow", >= 80 and <= 82 => "Showers", >= 85 and <= 86 => "Snow showers", >= 95 => "Thunderstorms", _ => "Weather" };
    public void Dispose() { if (trackedSession != null) trackedSession.PlaybackInfoChanged -= SessionPlaybackChanged; if (Media != null) Media.CurrentSessionChanged -= CurrentSessionChanged; }
}
