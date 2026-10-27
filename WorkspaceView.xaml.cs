using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Island;
public partial class WorkspaceView : UserControl, IDisposable
{
    internal static readonly string[] Sections = { "Home", "Now Playing", "Shelf", "Clipboard", "Notes", "Focus", "Search", "Snippets", "Launcher", "Reminders", "Camera & OCR", "Mixer", "AI Usage", "Developer", "System", "Teleprompter", "Capabilities" };
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer saveDelay = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer prompter = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private EventHandler? scrollHandler;
    private bool disposed;
    private string section = "Home";
    private TextBlock? liveText;
    private Action? tickPage;
    private CameraMirror? camera;
    private readonly List<IDisposable> audioSessions = new();
    private readonly IntPtr previousWindow;
    private static WorkspaceData D => WorkspaceData.Current;
    private static WorkspaceServices Hub => App.Workspace;
    internal WorkspaceView(string initial = "Home")
    {
        previousWindow = DesktopNative.GetForegroundWindow(); InitializeComponent();
        foreach (string name in Sections)
        {
            var button = new Button { Content = Icons.Label(name), Tag = name, Background = Brushes.Transparent, Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(0, 2, 0, 2), FontSize = 11, HorizontalContentAlignment = HorizontalAlignment.Left };
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            button.Click += (_, _) => { CommandSearch.Clear(); Select(name); }; Navigation.Children.Add(button);
        }
        CommandSearch.TextChanged += (_, _) => { if (CommandSearch.Text.Length > 0) Commands(CommandSearch.Text); else Select(section); };
        CommandSearch.KeyDown += (_, e) => { if (e.Key == Key.Escape && CommandSearch.Text.Length > 0) { CommandSearch.Clear(); e.Handled = true; } };
        refresh.Tick += (_, _) => { tickPage?.Invoke(); if (liveText != null && section == "System") liveText.Text = DesktopNative.SystemStats(); }; refresh.Start();
        saveDelay.Tick += (_, _) => { saveDelay.Stop(); try { D.Save(); } catch (Exception ex) { StatusLine.Text = "Could not save: " + ex.Message; } };
        PreviewKeyDown += (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.D1 && e.Key <= Key.D9) { Select(Sections[(int)e.Key - (int)Key.D1]); e.Handled = true; } };
        Select(initial == "Commands" ? "Home" : initial);
        if (initial == "Commands") Commands("");
        Icons.Apply(this);
    }
    internal void Select(string name)
    {
        if (saveDelay.IsEnabled) { saveDelay.Stop(); try { D.Save(); } catch (Exception ex) { StatusLine.Text = ex.Message; } }
        section = name; camera?.Dispose(); camera = null; prompter.Stop(); if (scrollHandler != null) { prompter.Tick -= scrollHandler; scrollHandler = null; } foreach (var item in audioSessions) item.Dispose(); audioSessions.Clear(); tickPage = null; liveText = null;
        Body.Children.Clear(); StatusLine.Text = ""; PageTitle.Text = name; PageSubtitle.Text = name switch { "Home" => "A quiet place for the things you reach for.", "Shelf" => "Drop it here. Pick it up anywhere.", "Clipboard" => "Your recent copies, close at hand.", "Focus" => "Time for the work that matters.", "Now Playing" => "Your soundtrack, without switching windows.", _ => "Your workspace, within reach." };
        foreach (var button in Navigation.Children.OfType<Button>()) button.Background = (string)button.Tag == name ? new SolidColorBrush(Color.FromRgb(51, 51, 60)) : Brushes.Transparent;
        switch (name)
        {
            case "Home": Home(); break; case "Now Playing": Media(); break; case "Shelf": Shelf(); break; case "Clipboard": Clips(); break; case "Notes": Notes(); break;
            case "Focus": FocusPage(); break; case "Search": SearchPage(); break; case "Snippets": Snippets(); break; case "Launcher": Launcher(); break; case "Reminders": Reminders(); break;
            case "Camera & OCR": CameraPage(); break; case "Mixer": Mixer(); break; case "AI Usage": Usage(); break; case "Developer": Developer(); break; case "System": SystemPage(); break; case "Teleprompter": Teleprompter(); break; default: Capabilities(); break;
        }
    }
    private StackPanel Card(string title)
    {
        var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        Body.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(150, 40, 40, 47)), BorderBrush = new SolidColorBrush(Color.FromArgb(23, 255, 255, 255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 16), Child = content }); return content;
    }
    private static TextBlock Text(Panel panel, string value, double size = 12)
    {
        var text = new TextBlock { Text = value, FontSize = size, Foreground = new SolidColorBrush(Color.FromRgb(164, 164, 180)), TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.55, Margin = new Thickness(0, 4, 0, 10) }; panel.Children.Add(text); return text;
    }
    private Button Button(Panel panel, string label, Action action)
    {
        var button = new Button { Content = Icons.Label(label), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 6, 8, 4) }; button.Click += (_, _) => { try { action(); } catch (Exception ex) { StatusLine.Text = ex.Message; } }; System.Windows.Automation.AutomationProperties.SetName(button, label); panel.Children.Add(button); return button;
    }
    private Button AsyncButton(Panel panel, string label, Func<Task> action)
    {
        var button = new Button { Content = Icons.Label(label), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 6, 8, 4) }; button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } catch (Exception ex) { StatusLine.Text = ex.Message; } finally { button.IsEnabled = true; } }; System.Windows.Automation.AutomationProperties.SetName(button, label); panel.Children.Add(button); return button;
    }
    private static TextBox Input(Panel panel, string value = "", string label = "Text", bool multiline = false)
    {
        var input = new TextBox { Text = value, MaxLength = multiline ? 1_000_000 : 20_000, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 160 : 0, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden };
        System.Windows.Automation.AutomationProperties.SetName(input, label); panel.Children.Add(input); return input;
    }
    private static WrapPanel Row(Panel parent) { var panel = new WrapPanel { Orientation = Orientation.Horizontal }; parent.Children.Add(panel); return panel; }
    private void Toggle(Panel parent, string label, bool value, Action<bool> changed)
    {
        var toggle = new CheckBox { Content = label, IsChecked = value }; toggle.Checked += (_, _) => Change(true); toggle.Unchecked += (_, _) => Change(false); parent.Children.Add(toggle);
        void Change(bool state) { try { changed(state); App.Prefs.Save(); App.Island.ApplyPreferences(); } catch (Exception ex) { StatusLine.Text = ex.Message; } }
    }
    private void ChangedText() { saveDelay.Stop(); saveDelay.Start(); }
    private void Home()
    {
        var greeting = Card(DateTime.Now.ToString("dddd, MMMM d")); liveText = Text(greeting, DateTime.Now.ToString("HH:mm"), 48);
        Text(greeting, "Music, files, focus and your next thought. All in one place."); var shortcuts = Row(greeting);
        foreach (string page in new[] { "Shelf", "Notes", "Focus", "Camera & OCR" }) Button(shortcuts, page, () => Select(page));
        var status = Card("At a glance"); var line = Text(status, ""); tickPage = () => { if (liveText != null) liveText.Text = DateTime.Now.ToString("HH:mm"); line.Text = Hub.Status + "\n" + App.Island.PowerText.Text + "\n" + App.Island.Services.WeatherSummary + "\n" + (Hub.StopwatchRunning ? "Stopwatch " + Hub.StopwatchText : ""); }; tickPage();
        var quick = Card("Quick note"); var note = Input(quick, D.Notes, "Quick note", true); note.TextChanged += (_, _) => { D.Notes = note.Text; ChangedText(); };
        var notifications = Card("Notification mirror · this session");
        Text(notifications, D.NotificationHistory.Count == 0 ? "Connect Windows notifications in Settings > Focus. Incoming notifications can appear here; inline replies are not supported by Windows' listener API." : string.Join("\n\n", D.NotificationHistory.Take(10)));
    }
    internal void ShowCommands() { CommandSearch.Clear(); Commands(""); }
    private void Commands(string query)
    {
        camera?.Dispose(); camera = null; prompter.Stop(); if (scrollHandler != null) { prompter.Tick -= scrollHandler; scrollHandler = null; } tickPage = null; foreach (var session in audioSessions) session.Dispose(); audioSessions.Clear();
        Body.Children.Clear(); PageTitle.Text = "Command palette"; PageSubtitle.Text = "Search a tool or type a calculation. Ctrl+Alt+K from anywhere.";
        var list = Card("Jump to"); foreach (string page in Sections.Where(p => p.Contains(query, StringComparison.OrdinalIgnoreCase))) Button(list, page, () => { CommandSearch.Clear(); Select(page); });
        if (query.Length > 0) try { string result = WorkspaceData.Calculate(query); Button(list, $"{query} = {result}  ·  Copy result", () => D.Copy(result)); } catch { }
        foreach (var snippet in D.Snippets.Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(10)) Button(list, "Snippet · " + snippet.Title, () => D.Copy(snippet.Text));
        var actions = Card("Quick actions");
        Button(actions, Hub.Awake ? "Stop Keep Awake" : "Keep Awake for 30 minutes", () => { Hub.KeepAwake(Hub.Awake ? 0 : 30); Commands(query); });
        Button(actions, "Start a 25-minute focus session", () => { App.Island.StartFocus(25); Select("Focus"); });
        Button(actions, "Toggle play / pause", () => _ = App.Island.MediaAction("play"));
        Button(actions, "Capture a screenshot", () => DesktopNative.Open("ms-screenclip:"));
    }
    private void Media()
    {
        var media = Card("Now Playing"); var title = Text(media, "", 24); var artist = Text(media, ""); var row = Row(media);
        Button(row, "Previous", () => _ = App.Island.MediaAction("previous")); var play = Button(row, "Play", () => _ = App.Island.MediaAction("play")); Button(row, "Next", () => _ = App.Island.MediaAction("next"));
        var extra = Row(media); var shuffle = Button(extra, "Shuffle", () => _ = App.Island.MediaAction("shuffle")); var repeat = Button(extra, "Repeat track", () => _ = App.Island.MediaAction("repeat")); var speed = Button(extra, "Playback speed", () => _ = App.Island.MediaAction("speed"));
        tickPage = () => { var service = App.Island.Services; title.Text = App.Island.TrackTitle.Text; artist.Text = App.Island.TrackArtist.Text; Icons.SetLabel(play, service.IsPlaying ? "Pause" : "Play"); shuffle.IsEnabled = service.CanShuffle; repeat.IsEnabled = service.CanRepeat; speed.IsEnabled = service.CanSpeed; Icons.SetLabel(shuffle, service.Shuffle == true ? "Shuffle on" : "Shuffle off"); }; tickPage();
        Text(media, "Shuffle, repeat and speed follow what the current app exposes through Windows media controls. Apple Music favorites require a separate account integration.");
        var lyrics = Card("Synced lyrics"); Text(lyrics, "Loading sends the current title, artist and duration to LRCLIB. No lyrics requests are made automatically."); var lyricText = Text(lyrics, "", 18); var load = AsyncButton(lyrics, "Load lyrics for this track", async () =>
        {
            var service = App.Island.Services; if (string.IsNullOrWhiteSpace(service.TrackName)) throw new InvalidOperationException("Play a track first.");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) }; client.DefaultRequestHeaders.UserAgent.ParseAdd("IslandDesktop/1.3");
            string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(service.TrackName)}&artist_name={Uri.EscapeDataString(service.ArtistName)}&duration={(int)service.DurationSeconds}";
            using var document = JsonDocument.Parse(await client.GetStringAsync(url)); string? synced = document.RootElement.TryGetProperty("syncedLyrics", out var value) ? value.GetString() : null;
            if (string.IsNullOrWhiteSpace(synced)) { lyricText.Text = "No synced lyrics found for this recording."; return; }
            var lines = Lyrics.Parse(synced); var oldTick = tickPage; tickPage = () => { oldTick?.Invoke(); lyricText.Text = Lyrics.At(lines, service.PositionSeconds); };
        });
    }
    private void Shelf()
    {
        var card = Card("File shelf"); Text(card, "Drop files onto the island or add them here. Shelf entries persist as references to the original files; removing an entry does not delete the original.");
        var row = Row(card); Button(row, "Add files…", () => { var dialog = new OpenFileDialog { Multiselect = true }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { D.AddFiles(dialog.FileNames); Select("Shelf"); } });
        Button(row, "Screenshot…", () => DesktopNative.Open("ms-screenclip:"));
        Button(row, "Stage clipboard image", () => { var image = Clipboard.GetImage() ?? throw new InvalidOperationException("Copy an image first."); string folder = Path.Combine(Preferences.Folder, "Shelf"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png"); CaptureTools.SaveImage(image, path, "PNG"); D.AddFiles(new[] { path }); Select("Shelf"); });
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Extended, Foreground = Brushes.White, MinHeight = 80, MaxHeight = 300 }; card.Children.Add(list);
        foreach (string file in D.Files) { var item = new ListBoxItem { Content = (File.Exists(file) ? "" : "Missing · ") + Path.GetFileName(file), Tag = file, Padding = new Thickness(10), ToolTip = file }; list.Items.Add(item); }
        list.PreviewMouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && list.SelectedItems.Count > 0 && Keyboard.Modifiers == ModifierKeys.Alt) { string[] paths = list.SelectedItems.Cast<ListBoxItem>().Select(i => (string)i.Tag).Where(File.Exists).ToArray(); if (paths.Length > 0) DragDrop.DoDragDrop(list, new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy); } };
        Text(card, "Select files, then hold Alt and drag to another app.");
        string[] Selected() => list.SelectedItems.Cast<ListBoxItem>().Select(i => (string)i.Tag).ToArray();
        var actions = Row(card); Button(actions, "Open", () => { var path = Selected().FirstOrDefault() ?? throw new InvalidOperationException("Select a file."); DesktopNative.Open(path); });
        Button(actions, "Reveal", () => { var path = Selected().FirstOrDefault() ?? throw new InvalidOperationException("Select a file."); var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false }; info.ArgumentList.Add("/select,"); info.ArgumentList.Add(path); Process.Start(info); });
        Button(actions, "Remove entry", () => { foreach (var path in Selected()) D.Files.Remove(path); D.Save(); Select("Shelf"); });
        var archives = Card("Convert & archive"); var format = new ComboBox { ItemsSource = new[] { "PNG", "JPEG", "TIFF", "BMP", "PDF" }, SelectedIndex = 0 }; archives.Children.Add(format); Text(archives, "PDF input converts its first page. HEIC input needs the Windows HEIF codec; HEIC export is not included.");
        AsyncButton(archives, "Convert selected image…", async () => { string path = Selected().FirstOrDefault() ?? throw new InvalidOperationException("Select an image."); string type = (string)format.SelectedItem; var dialog = new SaveFileDialog { Filter = $"{type}|*.{(type == "JPEG" ? "jpg" : type.ToLowerInvariant())}", FileName = Path.GetFileNameWithoutExtension(path), OverwritePrompt = true }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { await CaptureTools.ConvertImageAsync(path, dialog.FileName, type); D.AddFiles(new[] { dialog.FileName }); Select("Shelf"); } });
        Button(archives, "Zip selected files…", () => { var paths = Selected(); if (paths.Length == 0) throw new InvalidOperationException("Select files first."); var dialog = new SaveFileDialog { Filter = "ZIP archive|*.zip", FileName = "Archive.zip" }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { if (File.Exists(dialog.FileName)) throw new IOException("Choose a new archive filename."); WorkspaceData.ZipFiles(paths, dialog.FileName); D.AddFiles(new[] { dialog.FileName }); Select("Shelf"); } });
        Button(archives, "Unzip selected archive…", () => { string archive = Selected().FirstOrDefault() ?? throw new InvalidOperationException("Select a ZIP file."); var dialog = new OpenFolderDialog { Title = "Choose the parent output folder" }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { string destination = Path.Combine(dialog.FolderName, Path.GetFileNameWithoutExtension(archive) + "-extracted"); WorkspaceData.Extract(archive, destination); DesktopNative.Open(destination); } });
    }
    private void Clips()
    {
        var card = Card("Clipboard history"); Toggle(card, "Collect copied text while Island is running", App.Prefs.ClipboardHistory, value => App.Prefs.ClipboardHistory = value);
        Text(card, "Opt-in. Recent text stays in memory; only pinned clips are saved locally. Passwords and other sensitive text can be copied by apps, so enable this only when you want history. Respecting exclusion flags depends on the source app.");
        var search = Input(card, "", "Search clipboard"); var list = new StackPanel(); card.Children.Add(list);
        void Populate()
        {
            list.Children.Clear(); foreach (var clip in D.PinnedClips.Concat(D.Clips.Where(c => !D.PinnedClips.Any(p => p.Text == c.Text))).Where(c => c.Text.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).Take(100).ToArray())
            {
                var row = new StackPanel { Margin = new Thickness(0, 10, 0, 10) }; list.Children.Add(row); Text(row, clip.Text[..Math.Min(clip.Text.Length, 260)]); var buttons = Row(row);
                Button(buttons, "Copy", () => D.Copy(clip.Text)); Button(buttons, D.PinnedClips.Contains(clip) ? "Unpin" : "Pin", () => { if (!D.PinnedClips.Remove(clip)) { if (D.PinnedClips.Count >= 200) throw new IOException("The pin limit is 200. Unpin an item first."); D.PinnedClips.Add(clip); } D.Save(); Populate(); });
                string smart = WorkspaceData.SmartAction(clip.Text); if (smart == "Open link") Button(buttons, smart, () => DesktopNative.Open(clip.Text.Trim())); else if (smart == "Compose email") Button(buttons, smart, () => DesktopNative.Open("mailto:" + clip.Text.Trim())); else if (smart == "Color") { var swatch = new Border { Width = 22, Height = 22, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(clip.Text.Trim())), CornerRadius = new CornerRadius(5), Margin = new Thickness(8) }; buttons.Children.Add(swatch); }
            }
        }
        search.TextChanged += (_, _) => Populate(); Populate(); Button(card, "Refresh", Populate); Button(card, "Clear recent history · keep pins", () => { D.Clips.Clear(); Populate(); });
    }
    private void Notes()
    {
        var card = Card("Quick notes"); Text(card, "Saved automatically on this PC. No account or cloud upload."); var input = Input(card, D.Notes, "Quick notes", true); input.MinHeight = 380; input.FontSize = 15; input.TextChanged += (_, _) => { D.Notes = input.Text; ChangedText(); };
    }
    private void FocusPage()
    {
        var timer = Card("Pomodoro"); var display = Text(timer, App.Island.TimerText.Text, 48); var row = Row(timer);
        Button(row, "Start 25 min", () => App.Island.StartFocus(25)); Button(row, "Break 5 min", () => App.Island.StartFocus(5)); Button(row, "Pause / resume", () => App.Island.TimerToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)));
        Toggle(timer, "Chain focus sessions and 5-minute breaks", App.Prefs.ChainPomodoro, value => App.Prefs.ChainPomodoro = value);
        var history = Card("Last seven days"); foreach (var day in Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6))) Text(history, $"{day:ddd}    {D.FocusHistory.Where(f => f.Date.Date == day).Sum(f => f.Minutes)} focus minutes");
        var stopwatch = Card("Stopwatch"); var watch = Text(stopwatch, Hub.StopwatchText, 32); var controls = Row(stopwatch); Button(controls, "Start / pause", Hub.ToggleStopwatch); Button(controls, "Reset", Hub.ResetStopwatch);
        var awake = Card("Keep Awake"); var state = Text(awake, Hub.Status); var awakeRow = Row(awake);
        foreach (int minutes in new[] { 15, 30, 60, -1, 0 }) Button(awakeRow, minutes == -1 ? "Until stopped" : minutes == 0 ? "Stop" : $"{minutes} min", () => { Hub.KeepAwake(minutes); state.Text = Hub.Status; });
        var until = Input(awake, DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"), "Keep awake until"); Button(awake, "Keep awake until this time", () => { if (!DateTime.TryParseExact(until.Text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var deadline)) throw new ArgumentException("Use yyyy-MM-dd HH:mm."); Hub.KeepAwakeUntil(deadline); });
        tickPage = () => { display.Text = App.Island.TimerText.Text; watch.Text = Hub.StopwatchText; state.Text = Hub.Status; };
    }
    private void SearchPage()
    {
        var web = Card("Search the web"); var query = Input(web, "", "Search query"); string[] names = { "Google", "Bing", "DuckDuckGo", "Brave", "Startpage", "Kagi", "Ecosia", "YouTube", "Wikipedia" };
        string[] urls = { "https://www.google.com/search?q=", "https://www.bing.com/search?q=", "https://duckduckgo.com/?q=", "https://search.brave.com/search?q=", "https://www.startpage.com/sp/search?query=", "https://kagi.com/search?q=", "https://www.ecosia.org/search?q=", "https://www.youtube.com/results?search_query=", "https://en.wikipedia.org/w/index.php?search=" };
        var engine = new ComboBox { ItemsSource = names, SelectedIndex = 0 }; web.Children.Add(engine); Button(web, "Search", () => DesktopNative.Open(urls[engine.SelectedIndex] + Uri.EscapeDataString(query.Text)));
        var translate = Card("Translate"); var text = Input(translate, "", "Text to translate", true); Text(translate, "This opens your text in Google Translate when you click Translate. Text is not sent while typing."); Button(translate, "Translate in browser", () => DesktopNative.Open("https://translate.google.com/?sl=auto&tl=en&text=" + Uri.EscapeDataString(text.Text) + "&op=translate"));
        var math = Card("Calculator"); var expression = Input(math, "", "Calculation"); var result = Text(math, ""); Button(math, "Calculate", () => { string answer = WorkspaceData.Calculate(expression.Text); result.Text = answer; D.MathHistory.Insert(0, expression.Text + " = " + answer); D.MathHistory = D.MathHistory.Take(50).ToList(); D.Save(); }); Text(math, string.Join("\n", D.MathHistory.Take(10)));
    }
    private void Snippets()
    {
        var card = Card("Reusable text"); var title = Input(card, "", "Snippet name"); var text = Input(card, "", "Snippet text", true); Button(card, "Save snippet", () => { if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(text.Text)) throw new ArgumentException("Enter a name and text."); D.Snippets.Add(new SavedText { Title = title.Text, Text = text.Text }); D.Save(); Select("Snippets"); });
        foreach (var snippet in D.Snippets.ToArray()) { var row = Card(snippet.Title); Text(row, snippet.Text[..Math.Min(300, snippet.Text.Length)]); var buttons = Row(row); Button(buttons, "Copy · without adding to history", () => D.Copy(snippet.Text)); Button(buttons, "Remove", () => { D.Snippets.Remove(snippet); D.Save(); Select("Snippets"); }); }
    }
    private void Launcher()
    {
        var card = Card("Apps & Windows shortcuts"); Text(card, "Pin apps, .lnk shortcuts and .url internet shortcuts. They run only when you click Launch.");
        Button(card, "Add app or shortcut…", () => { var dialog = new OpenFileDialog { Filter = "Apps and shortcuts|*.exe;*.lnk;*.url" }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { D.Launchers.Add(new SavedText { Title = Path.GetFileNameWithoutExtension(dialog.FileName), Text = dialog.FileName }); D.Save(); Select("Launcher"); } });
        foreach (var app in D.Launchers.ToArray()) { var row = Card(app.Title); var controls = Row(row); Button(controls, "Launch", () => { if (!File.Exists(app.Text)) throw new IOException("The pinned file has moved."); DesktopNative.Open(app.Text); }); Button(controls, "Move up", () => { int i = D.Launchers.IndexOf(app); if (i > 0) (D.Launchers[i - 1], D.Launchers[i]) = (D.Launchers[i], D.Launchers[i - 1]); D.Save(); Select("Launcher"); }); Button(controls, "Remove", () => { D.Launchers.Remove(app); D.Save(); Select("Launcher"); }); }
    }
    private void Reminders()
    {
        var card = Card("Local reminders"); Text(card, "Delivered while Island is running. This is a local Windows equivalent; it does not sync to Apple Reminders/iCloud."); var title = Input(card, "", "Reminder"); var date = Input(card, DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"), "Due date");
        Button(card, "Add reminder", () => { if (string.IsNullOrWhiteSpace(title.Text) || !DateTime.TryParseExact(date.Text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)) throw new ArgumentException("Enter text and a date as yyyy-MM-dd HH:mm."); D.Reminders.Add(new Reminder { Title = title.Text.Trim(), When = when }); D.Save(); Select("Reminders"); });
        foreach (var reminder in D.Reminders.ToArray()) { var row = Card(reminder.Title); Text(row, reminder.Done ? "Completed" : reminder.When.ToString("f")); Button(row, reminder.Done ? "Remove" : "Complete", () => { if (reminder.Done) D.Reminders.Remove(reminder); else reminder.Done = true; D.Save(); Select("Reminders"); }); }
    }
    private void CameraPage()
    {
        var mirror = Card("Camera mirror"); Text(mirror, "Starts only when requested. Windows may ask for camera permission. Video stays on this PC; leaving this page stops the camera."); var image = new Image { Height = 280, Stretch = Stretch.Uniform, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(-1, 1) }; mirror.Children.Add(image);
        AsyncButton(mirror, "Start mirror", async () => { camera?.Dispose(); camera = new CameraMirror(image); await camera.Start(); }); Button(mirror, "Stop camera", () => { camera?.Dispose(); camera = null; image.Source = null; });
        var ocr = Card("Read text from an image"); var result = Input(ocr, "", "OCR result", true); AsyncButton(ocr, "Read clipboard image", async () => { var bitmap = Clipboard.GetImage() ?? throw new InvalidOperationException("Copy a screenshot or image first."); result.Text = await CaptureTools.Ocr(bitmap); }); Button(ocr, "Copy recognized text", () => D.Copy(result.Text)); Button(ocr, "Take a screenshot…", () => DesktopNative.Open("ms-screenclip:"));
    }
    private void Mixer()
    {
        var card = Card("Application mixer"); Text(card, "Controls each Windows audio session without changing master volume. Apps appear once they create an audio session. Three-band per-app EQ requires an audio processing driver and is not implemented.");
        Button(card, "Refresh sessions", () => Select("Mixer"));
        try
        {
            foreach (var session in AppMixer.Enumerate())
            {
                audioSessions.Add(session); var app = Card(session.Name); var slider = new Slider { Value = session.Volume * 100 }; app.Children.Add(slider); System.Windows.Automation.AutomationProperties.SetName(slider, session.Name + " volume");
                slider.ValueChanged += (_, _) => { try { session.SetVolume((float)slider.Value / 100); } catch (Exception ex) { StatusLine.Text = ex.Message; } };
                var mute = new CheckBox { Content = "Mute this app", IsChecked = session.Muted }; mute.Checked += (_, _) => session.SetMute(true); mute.Unchecked += (_, _) => session.SetMute(false); app.Children.Add(mute);
            }
        }
        catch (Exception ex) { Text(card, ex.Message); }
        Button(card, "Choose output device in Windows", () => DesktopNative.Open("ms-settings:sound")); Button(card, "Toggle default microphone mute", () => Audio.ToggleMicrophone());
    }
    private void Usage()
    {
        var card = Card("AI usage · local logs"); Text(card, "Choose a log file explicitly. Island does not scan credentials or assume prices. Codex token-count events and Claude usage events are recognized; normalized logs can supply Cursor/Copilot usage. Missing cost or limit information is shown as unavailable.");
        var provider = new ComboBox { ItemsSource = new[] { "Codex", "Claude Code", "Cursor", "Copilot" }, SelectedIndex = 0 }; card.Children.Add(provider);
        Button(card, "Choose JSON/JSONL log…", () => { var dialog = new OpenFileDialog { Filter = "JSON logs|*.json;*.jsonl|All files|*.*" }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { D.UsageFiles.RemoveAll(f => f.Title == (string)provider.SelectedItem); D.UsageFiles.Add(new SavedText { Title = (string)provider.SelectedItem, Text = dialog.FileName }); D.Save(); Select("AI Usage"); } });
        foreach (var file in D.UsageFiles.ToArray()) { var usage = Card(file.Title); Text(usage, UsageReader.Read(file.Text)); var row = Row(usage); Button(row, "Move up", () => { int i = D.UsageFiles.IndexOf(file); if (i > 0) (D.UsageFiles[i - 1], D.UsageFiles[i]) = (D.UsageFiles[i], D.UsageFiles[i - 1]); D.Save(); Select("AI Usage"); }); Button(row, "Hide provider", () => { D.UsageFiles.Remove(file); D.Save(); Select("AI Usage"); }); }
        Text(Card("Live agent reports"), "Your own scripts can post token/cost activity through the authenticated Developer API. Agent hooks are not installed automatically.");
    }
    private void Developer()
    {
        var card = Card("Local activity API"); Toggle(card, "Enable loopback API on port 9999", App.Prefs.DeveloperApi, value => { App.Prefs.DeveloperApi = value; Hub.Configure(); }); Text(card, Hub.Api.Status);
        Text(card, "Off by default. Loopback only; every request needs Authorization: Bearer <token>. The token is encrypted for your Windows account using DPAPI. No browser CORS access. Use POST /activities to report build, agent, download, usage or approval activity; GET /activities reads updates/decisions.");
        Button(card, "Copy per-install API token", () => D.Copy(Hub.Api.Token));
        Text(card, "Approval reports are for integrations you configure. Allow/Deny decisions are returned through GET; they do not bypass Codex/Claude's own permission system.");
        var reports = Card("Live activities"); var list = new StackPanel(); reports.Children.Add(list);
        void Populate()
        {
            list.Children.Clear(); foreach (var activity in Hub.Api.Activities.Values.OrderByDescending(a => a.Kind == "approval").ThenByDescending(a => a.Updated))
            {
                Text(list, activity.Title + (activity.Progress.HasValue ? $" · {activity.Progress:P0}" : "") + (activity.Tokens.HasValue ? $" · {activity.Tokens:N0} tokens" : "") + (activity.CostUsd.HasValue ? $" · ${activity.CostUsd:0.0000}" : ""));
                var row = Row(list); if (activity.Kind == "approval" && activity.Decision == null) { Button(row, "Allow", () => { activity.Decision = "allow"; Populate(); }); Button(row, "Deny", () => { activity.Decision = "deny"; Populate(); }); } else if (activity.Decision != null) Text(list, "Decision: " + activity.Decision);
                Button(row, "Dismiss", () => { Hub.Api.Activities.TryRemove(activity.Id, out _); Populate(); });
            }
        }
        Populate(); Button(reports, "Refresh reports", Populate);
    }
    private void SystemPage()
    {
        var stats = Card("System stats"); liveText = Text(stats, DesktopNative.SystemStats(), 20);
        var privacy = Card("Privacy & access"); Toggle(privacy, "Exclude Island from supported screen captures", App.Prefs.HideFromCapture, value => { App.Prefs.HideFromCapture = value; DesktopNative.ApplyCapturePrivacy(Window.GetWindow(this) ?? App.Island); });
        Toggle(privacy, "Download completion alerts", App.Prefs.DownloadAlerts, value => { App.Prefs.DownloadAlerts = value; Hub.Configure(); }); Toggle(privacy, "Caps Lock HUD", App.Prefs.CapsLockHud, value => App.Prefs.CapsLockHud = value);
        Text(privacy, "Capture exclusion is a Windows best-effort request; unsupported capture methods can still see the app. This is not a system-wide camera/mic privacy detector.");
        Button(privacy, "Set up Windows Hello face unlock", () => DesktopNative.Open("ms-settings:signinoptions")); Button(privacy, "Camera privacy settings", () => DesktopNative.Open("ms-settings:privacy-webcam")); Button(privacy, "Microphone privacy settings", () => DesktopNative.Open("ms-settings:privacy-microphone"));
        var snap = Card("Window snapping"); Text(snap, "Tiles the window that was active before this workspace opened. The workspace does not inject itself into another app's drag loop."); var zones = Row(snap); foreach (string zone in new[] { "Left", "Right", "Top", "Bottom", "Maximize" }) Button(zones, zone, () => DesktopNative.SnapWindow(previousWindow, zone));
        var cleaning = Card("Keyboard cleaning lock"); Text(cleaning, "Blocks ordinary keys for 20 seconds. Ctrl+Alt+Esc exits immediately; Windows secure attention keys remain available. Lock automatically expires even if the interface is busy."); var countdown = Text(cleaning, ""); Button(cleaning, "Lock keys for 20 seconds", VolumeKeyHook.CleanKeyboard); var oldTick = tickPage; tickPage = () => { oldTick?.Invoke(); countdown.Text = VolumeKeyHook.CleanSeconds > 0 ? $"Keys locked · {VolumeKeyHook.CleanSeconds}s remaining" : "Keyboard unlocked"; };
        var display = Card("External displays · DDC/CI"); Text(display, "Opt-in query. Your monitor must expose DDC/CI brightness, and may need DDC/CI enabled in its menu. These calls can take several seconds.");
        AsyncButton(display, "Find compatible monitors", async () =>
        {
            var monitors = await Task.Run(ExternalDisplay.Read); if (monitors.Count == 0) { Text(display, "No DDC/CI brightness controls found."); return; }
            foreach (var monitor in monitors) { Text(display, monitor.Name); var slider = new Slider { Value = monitor.Value }; display.Children.Add(slider); System.Windows.Automation.AutomationProperties.SetName(slider, monitor.Name + " brightness"); AsyncButton(display, "Apply brightness", async () => { await Task.Run(() => ExternalDisplay.Set(monitor.Name, (int)slider.Value)); App.Island.ShowHud("☀", "Display brightness", monitor.Name, (int)slider.Value, true); }); }
        });
        Button(privacy, "Pick a screen color…", () => new ColorPickerWindow(color => { D.Copy(color); App.Island.ShowHud("✓", "Color copied", color, allowInFocus: true); }).Show());
        var drives = Card("Removable drives"); foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable)) { var row = Row(drives); Text(row, drive.Name); Button(row, "Open", () => DesktopNative.Open(drive.RootDirectory.FullName)); Button(row, "Safely remove via Windows", () => DesktopNative.Open("ms-settings:connecteddevices")); }
        var downloads = Card("Completed downloads · this session"); foreach (string path in D.DownloadHistory.Take(10)) Button(downloads, Path.GetFileName(path), () => DesktopNative.Open(Path.GetDirectoryName(path)!));
    }
    private void Teleprompter()
    {
        var card = Card("Webcam-aligned teleprompter"); Text(card, "Detach this page and move it below your webcam. Script stays local. No Zoom integration or recording is required."); var script = Input(card, D.Script, "Teleprompter script", true); script.TextChanged += (_, _) => { D.Script = script.Text; ChangedText(); };
        var viewer = new ScrollViewer { Height = 300, Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = new TextBlock { Text = D.Script, FontSize = 30, TextWrapping = TextWrapping.Wrap, LineHeight = 48, Foreground = Brushes.White } }; card.Children.Add(viewer);
        var speed = new Slider { Minimum = 5, Maximum = 100, Value = 24 }; card.Children.Add(speed); System.Windows.Automation.AutomationProperties.SetName(speed, "Teleprompter scroll speed");
        scrollHandler = Scroll; prompter.Tick += scrollHandler;
        void Scroll(object? sender, EventArgs e) { viewer.ScrollToVerticalOffset(viewer.VerticalOffset + speed.Value / 30); }
        Button(card, "Start / pause", () => { if (prompter.IsEnabled) prompter.Stop(); else { ((TextBlock)viewer.Content).Text = script.Text; script.Visibility = Visibility.Collapsed; viewer.Visibility = Visibility.Visible; if (Window.GetWindow(this) is Window host) host.Topmost = true; prompter.Start(); } });
        Button(card, "Edit / reset", () => { prompter.Stop(); viewer.ScrollToTop(); viewer.Visibility = Visibility.Collapsed; script.Visibility = Visibility.Visible; if (Window.GetWindow(this) is Window host && host != App.Island) host.Topmost = false; });
    }
    private void Capabilities()
    {
        var card = Card("Windows feature map"); Text(card, "The original request describes macOS services. This app implements Windows equivalents where possible. Items that need a provider, driver or secure-desktop integration are not presented as working controls.");
        foreach (var line in FeatureMap.Lines) Text(card, line);
    }
    internal void Suspend()
    {
        refresh.Stop(); prompter.Stop(); camera?.Dispose(); camera = null;
        foreach (var session in audioSessions) session.Dispose(); audioSessions.Clear(); tickPage = null;
        if (saveDelay.IsEnabled) { saveDelay.Stop(); try { D.Save(); } catch (Exception ex) { Log.Write(ex); } }
    }
    public void Dispose() { if (disposed) return; disposed = true; Suspend(); if (scrollHandler != null) prompter.Tick -= scrollHandler; }
    private void TitleDrag(object sender, MouseButtonEventArgs e) { if (Window.GetWindow(this) is Window host && host != App.Island && e.OriginalSource is Grid) host.DragMove(); }
    private void CloseClick(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window host && host != App.Island) host.Close(); else App.Island.CloseWorkspace(); }
    private void MinimizeClick(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window host && host != App.Island) host.WindowState = WindowState.Minimized; else App.Island.Collapse(); }
    private void DetachClick(object sender, RoutedEventArgs e)
    {
        string page = section; if (Window.GetWindow(this) == App.Island) App.Island.CloseWorkspace();
        var view = new WorkspaceView(page);
        var window = new Window { Title = "Island · " + page, Content = view, Width = 900, Height = 680, MinWidth = 650, MinHeight = 460, Background = Brushes.Transparent, AllowsTransparency = true, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResizeWithGrip, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.SourceInitialized += (_, _) => DesktopNative.ApplyCapturePrivacy(window); window.Closed += (_, _) => view.Dispose(); window.Show();
    }
}
