using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Island;
internal sealed class WorkspaceServices : IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private FileSystemWatcher? downloads;
    private DateTimeOffset? awakeUntil, stopwatchStarted;
    private TimeSpan stopwatchElapsed;
    private bool caps = (DesktopNative.GetKeyState(0x14) & 1) != 0;
    internal bool Awake => awakeUntil.HasValue;
    internal string StopwatchText => (stopwatchElapsed + (stopwatchStarted.HasValue ? DateTimeOffset.Now - stopwatchStarted.Value : TimeSpan.Zero)).ToString(@"hh\:mm\:ss");
    internal bool StopwatchRunning => stopwatchStarted.HasValue;
    internal string Status => Awake ? awakeUntil == DateTimeOffset.MaxValue ? "Awake · until you stop it" : $"Awake until {awakeUntil:t}" : "Keep Awake is off";
    internal LocalActivityServer Api { get; } = new();
    internal WorkspaceServices() { timer.Tick += (_, _) => Tick(); timer.Start(); }
    internal void Configure()
    {
        downloads?.Dispose(); downloads = null;
        if (App.Prefs.DownloadAlerts)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(folder)) { downloads = new FileSystemWatcher(folder) { NotifyFilter = NotifyFilters.FileName, EnableRaisingEvents = true }; downloads.Created += (_, e) => Downloaded(e.FullPath); downloads.Renamed += (_, e) => Downloaded(e.FullPath); }
        }
        if (App.Prefs.DeveloperApi) Api.Start(); else Api.Stop();
    }
    private async void Downloaded(string path)
    {
        if (new[] { ".crdownload", ".part", ".tmp", ".download" }.Contains(Path.GetExtension(path).ToLowerInvariant())) return;
        await System.Threading.Tasks.Task.Delay(2000);
        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (!File.Exists(path) || WorkspaceData.Current.DownloadHistory.Contains(path)) return;
            WorkspaceData.Current.DownloadHistory.Insert(0, path); if (WorkspaceData.Current.DownloadHistory.Count > 100) WorkspaceData.Current.DownloadHistory.RemoveAt(100);
            App.Island.ShowHud("✓", "Download ready", Path.GetFileName(path));
        });
    }
    internal void KeepAwake(int minutes)
    {
        awakeUntil = minutes < 0 ? DateTimeOffset.MaxValue : minutes == 0 ? null : DateTimeOffset.Now.AddMinutes(minutes);
        DesktopNative.SetThreadExecutionState(awakeUntil.HasValue ? 0x80000003u : 0x80000000u);
    }
    internal void ToggleStopwatch() { if (stopwatchStarted.HasValue) { stopwatchElapsed += DateTimeOffset.Now - stopwatchStarted.Value; stopwatchStarted = null; } else stopwatchStarted = DateTimeOffset.Now; }
    internal void KeepAwakeUntil(DateTimeOffset until) { if (until <= DateTimeOffset.Now || until > DateTimeOffset.Now.AddDays(7)) throw new ArgumentException("Choose a future time within seven days."); awakeUntil = until; DesktopNative.SetThreadExecutionState(0x80000003); }
    internal void ResetStopwatch() { stopwatchStarted = null; stopwatchElapsed = TimeSpan.Zero; }
    private void Tick()
    {
        if (awakeUntil < DateTimeOffset.Now) KeepAwake(0);
        bool nextCaps = (DesktopNative.GetKeyState(0x14) & 1) != 0;
        if (nextCaps != caps && App.Prefs.CapsLockHud) App.Island.ShowHud("!", nextCaps ? "Caps Lock on" : "Caps Lock off", allowInFocus: true); caps = nextCaps;
        foreach (var reminder in WorkspaceData.Current.Reminders.Where(r => !r.Done && r.When <= DateTime.Now).ToArray()) { reminder.Done = true; App.Island.ShowHud("!", "Reminder", reminder.Title, allowInFocus: true); WorkspaceData.Current.Save(); }
    }
    public void Dispose() { timer.Stop(); downloads?.Dispose(); KeepAwake(0); Api.Dispose(); }
}
