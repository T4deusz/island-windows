using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace Island;
public sealed class SavedText { public string Title { get; set; } = ""; public string Text { get; set; } = ""; public bool Pinned { get; set; } }
public sealed class Reminder { public string Title { get; set; } = ""; public DateTime When { get; set; } public bool Done { get; set; } }
public sealed class FocusRecord { public DateTime Date { get; set; } public int Minutes { get; set; } }
internal sealed class WorkspaceData
{
    internal static readonly WorkspaceData Current = Load();
    public string Notes { get; set; } = "";
    public string Script { get; set; } = "";
    public List<SavedText> PinnedClips { get; set; } = new();
    public List<SavedText> Snippets { get; set; } = new();
    public List<SavedText> Launchers { get; set; } = new();
    public List<string> Files { get; set; } = new();
    public List<string> MathHistory { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = new();
    public List<FocusRecord> FocusHistory { get; set; } = new();
    public List<SavedText> UsageFiles { get; set; } = new();
    internal readonly List<SavedText> Clips = new();
    internal readonly List<string> DownloadHistory = new();
    internal readonly List<string> NotificationHistory = new();
    internal bool SuppressClipboard;
    internal event Action? Changed;
    private static string PathName => Path.Combine(Preferences.Folder, "workspace.json");
    private static WorkspaceData Load()
    {
        try { var data = JsonSerializer.Deserialize<WorkspaceData>(File.ReadAllText(PathName)) ?? new(); data.Normalize(); return data; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    internal void Normalize()
    {
        Notes ??= ""; Script ??= ""; PinnedClips ??= new(); Snippets ??= new(); Launchers ??= new(); Files ??= new(); MathHistory ??= new(); Reminders ??= new(); FocusHistory ??= new(); UsageFiles ??= new();
        Notes = Notes[..Math.Min(Notes.Length, 1_000_000)]; Script = Script[..Math.Min(Script.Length, 1_000_000)];
        Files = Files.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(300).ToList();
        PinnedClips = PinnedClips.Where(c => c != null && !string.IsNullOrEmpty(c.Text)).Take(200).ToList();
        Snippets = Snippets.Where(c => c != null && !string.IsNullOrEmpty(c.Text)).Take(200).ToList();
        Reminders = Reminders.Where(c => c != null).Take(500).ToList(); FocusHistory = FocusHistory.Where(c => c != null && c.Date > DateTime.Today.AddDays(-370)).ToList();
    }
    internal void Save()
    {
        Normalize(); Directory.CreateDirectory(Preferences.Folder);
        File.WriteAllText(PathName + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(PathName + ".tmp", PathName, true); Changed?.Invoke();
    }
    internal void AddFiles(IEnumerable<string> files)
    {
        var added = files.Where(File.Exists).Select(Path.GetFullPath).Where(p => !Files.Contains(p, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (Files.Count + added.Length > 300) throw new IOException("The shelf holds up to 300 file references. Remove some entries first."); Files.AddRange(added); Save();
    }
    internal void CaptureClipboard()
    {
        if (SuppressClipboard) return;
        try
        {
            // Respect applications that explicitly exclude clipboard content from monitoring/history.
            if (Clipboard.GetDataObject() is not { } data || data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")) return;
            if (data.GetDataPresent("CanIncludeInClipboardHistory") && data.GetData("CanIncludeInClipboardHistory") is System.IO.MemoryStream flag && flag.ToArray().FirstOrDefault() == 0) return;
            if (!Clipboard.ContainsText()) return; string text = Clipboard.GetText(); if (string.IsNullOrWhiteSpace(text) || text.Length > 20_000) return;
            Clips.RemoveAll(c => c.Text == text); Clips.Insert(0, new SavedText { Text = text, Title = text[..Math.Min(text.Length, 90)].Replace('\n', ' ') }); if (Clips.Count > 100) Clips.RemoveRange(100, Clips.Count - 100); Changed?.Invoke();
        }
        catch (System.Runtime.InteropServices.COMException) { }
    }
    internal void Copy(string text)
    {
        SuppressClipboard = true; try { Clipboard.SetText(text); } finally { Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => SuppressClipboard = false); }
    }
    internal static string Calculate(string expression)
    {
        if (expression.Length > 500 || !Regex.IsMatch(expression, @"\A[0-9.eE+\-*/()% \t]+\z")) throw new ArgumentException("Use numbers, parentheses and + − * / % only.");
        using var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        var result = table.Compute(expression, ""); double numeric = Convert.ToDouble(result, CultureInfo.InvariantCulture); if (!double.IsFinite(numeric)) throw new ArgumentException("The result is not a finite number."); return Convert.ToString(result, CultureInfo.InvariantCulture) ?? "";
    }
    internal static string SmartAction(string text)
    {
        if (Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return "Open link";
        if (Regex.IsMatch(text.Trim(), @"\A[^\s@]+@[^\s@]+\.[^\s@]+\z")) return "Compose email";
        if (Regex.IsMatch(text.Trim(), @"\A#[0-9a-fA-F]{6}\z")) return "Color";
        return "Copy";
    }
    internal static void ZipFiles(IEnumerable<string> files, string target)
    {
        string temporary = target + ".island-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files.Where(File.Exists)) { string name = Path.GetFileName(file); if (!names.Add(name)) throw new IOException("Two selected files have the same name. Rename one before creating the archive."); zip.CreateEntryFromFile(file, name, CompressionLevel.Optimal); }
            }
            File.Move(temporary, target, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static void Extract(string archive, string destination)
    {
        using var zip = ZipFile.OpenRead(archive); string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        if (Directory.Exists(destination)) throw new IOException("Choose a new output folder to avoid overwriting existing files.");
        long total = 0; if (zip.Entries.Count > 5000) throw new IOException("Archive exceeds the 5,000 entry limit.");
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (name.Contains(':') || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !Path.GetFullPath(Path.Combine(root, name)).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Archive contains an unsafe path or symbolic link.");
            total = checked(total + entry.Length); if (total > 2L * 1024 * 1024 * 1024) throw new IOException("Archive exceeds the 2 GB expanded-size limit.");
        }
        ZipFile.ExtractToDirectory(archive, destination, false);
    }
}
