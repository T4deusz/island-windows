using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Island;
public sealed class AgendaItem
{
    public string Title { get; set; } = "";
    public DateTime When { get; set; }
}
public sealed class Preferences
{
    public bool Onboarded { get; set; }
    public bool VolumeHud { get; set; } = true;
    public bool BrightnessHud { get; set; } = true;
    public bool DeviceAlerts { get; set; } = true;
    public bool BatteryAlerts { get; set; } = true;
    public bool Notifications { get; set; }
    public bool ReducedMotion { get; set; }
    public string AnimationStyle { get; set; } = "Spring";
    public bool AccentFromArtwork { get; set; } = true;
    public bool Visualizer { get; set; } = true;
    public bool DebugFps { get; set; }
    public bool NotchMode { get; set; } = true;
    public bool HideFromCapture { get; set; }
    public bool ClipboardHistory { get; set; }
    public bool DownloadAlerts { get; set; }
    public bool CapsLockHud { get; set; } = true;
    public bool HorizontalGestures { get; set; } = true;
    public bool VerticalGestures { get; set; } = true;
    public bool ChainPomodoro { get; set; }
    public int FullChargeLevel { get; set; } = 100;
    public bool DeveloperApi { get; set; }
    public bool ReplaceVolumeKeys { get; set; } = true;
    public string HudPosition { get; set; } = "Below island";
    public List<int> WidgetOrder { get; set; } = new() { 0, 1, 2 };
    public bool HoverExpand { get; set; } = true;
    public bool AutoHide { get; set; } = true;
    public bool ShowPercentage { get; set; } = true;
    public bool Focus { get; set; }
    public bool Weather { get; set; }
    public string City { get; set; } = "Paris";
    public string Accent { get; set; } = "#83E2C6";
    public double HudSeconds { get; set; } = 2.5;
    public double Offset { get; set; } = 8;
    public double Scale { get; set; } = 1;
    public int Monitor { get; set; }
    public List<AgendaItem> Agenda { get; set; } = new();
    internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IslandDesktop");
    internal static string FilePath => Path.Combine(Folder, "settings.json");
    public static Preferences Load()
    {
        try { var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); p.Normalize(); return p; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Normalize()
    {
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .8, 1.5) : 1;
        FullChargeLevel = Math.Clamp(FullChargeLevel, 80, 100);
        Offset = double.IsFinite(Offset) ? Math.Clamp(Offset, 0, 150) : 8;
        HudSeconds = double.IsFinite(HudSeconds) ? Math.Clamp(HudSeconds, 1, 10) : 2.5;
        if (AnimationStyle is not ("Spring" or "Gentle" or "Instant")) AnimationStyle = "Spring";
        if (HudPosition is not ("Below island" or "Inline when collapsed")) HudPosition = "Below island";
        WidgetOrder = (WidgetOrder ?? new()).Where(x => x >= 0 && x <= 2).Distinct().Concat(new[] { 0, 1, 2 }).Distinct().ToList();
        City = string.IsNullOrWhiteSpace(City) ? "Paris" : City.Trim();
        Agenda ??= new(); Agenda = Agenda.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Title)).Take(500).ToList();
        try { if (System.Windows.Media.ColorConverter.ConvertFromString(Accent) is not System.Windows.Media.Color) Accent = "#83E2C6"; } catch { Accent = "#83E2C6"; }
    }
    public void Save()
    {
        try { Normalize(); Directory.CreateDirectory(Folder); File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(FilePath + ".tmp", FilePath, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write(ex); }
    }
}
internal static class Log
{
    public static void Write(Exception ex) { try { Directory.CreateDirectory(Preferences.Folder); var p = Path.Combine(Preferences.Folder, "island.log"); if (File.Exists(p) && new FileInfo(p).Length > 1_000_000) File.Delete(p); File.AppendAllText(p, $"{DateTimeOffset.Now:u} {ex.GetType().Name}: {ex.Message}\n"); } catch { } }
}
