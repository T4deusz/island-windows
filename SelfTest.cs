using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Island;
internal static class SelfTest
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("Self-test failed: " + name); count++; }
        var p = new Preferences { Scale = 100, Offset = -1, HudSeconds = double.NaN, Accent = "invalid", City = "  ", Agenda = new() { new() { Title = "", When = DateTime.Now }, new() { Title = "Meeting", When = DateTime.Now } } };
        p.Normalize(); Check(p.Scale == 1.5 && p.Offset == 0 && p.HudSeconds == 2.5, "numeric validation"); Check(p.Accent == "#83E2C6", "color validation"); Check(p.City == "Paris" && p.Agenda.Count == 1, "data validation");
        var roundtrip = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p))!; Check(roundtrip.Agenda.Single().Title == "Meeting", "settings round trip");
        Check(DesktopServices.WeatherName(0) == "Clear" && DesktopServices.WeatherName(95) == "Thunderstorms" && DesktopServices.WeatherName(71) == "Snow", "weather interpretation");
        var old = JsonSerializer.Deserialize<Preferences>("{\"VolumeHud\":false,\"Scale\":1.2}")!; old.Normalize(); Check(!old.VolumeHud && old.AccentFromArtwork && old.WidgetOrder.SequenceEqual(new[] { 0, 1, 2 }), "old preferences migrate without losing values");
        p.WidgetOrder = new() { 2, 2, 19 }; p.AnimationStyle = "unknown"; p.HudPosition = "unknown"; p.Normalize(); Check(p.WidgetOrder.SequenceEqual(new[] { 2, 0, 1 }) && p.AnimationStyle == "Spring" && p.HudPosition == "Below island", "new settings validation");
        var spring = new Spring { Target = 1 }; double maximum = 0;
        for (int i = 0; i < 240; i++) { spring.Step(1d / 60, Springs.Open); maximum = Math.Max(maximum, spring.Value); }
        Check(!spring.Active && spring.Value == 1 && maximum > 1 && maximum < 1.08, "spring settles with restrained overshoot");
        var slow = new Spring { Target = 1 }; var fast = new Spring { Target = 1 };
        for (int i = 0; i < 15; i++) slow.Step(1d / 30, Springs.Open); for (int i = 0; i < 72; i++) fast.Step(1d / 144, Springs.Open);
        Check(Math.Abs(slow.Value - fast.Value) < .006, "spring stable across refresh rates");
        double before = fast.Value; fast.Target = 0; Check(fast.Value == before, "interrupting motion keeps its current position");
        Check(Math.Abs(Spring.RubberBand(500)) < 120 && Spring.RubberBand(-50) == -Spring.RubberBand(50), "rubber-band bounds and symmetry");
        foreach (var config in new[] { Springs.Open, Springs.Close, Springs.Smooth, Springs.Snappy, Springs.Bouncy, Springs.Tracking, Springs.Release })
        {
            var lowRate = new Spring { Target = 1 }; var highRate = new Spring { Target = 1 };
            for (int i = 0; i < 30; i++) lowRate.Step(1d / 30, config);
            for (int i = 0; i < 144; i++) highRate.Step(1d / 144, config);
            Check(double.IsFinite(lowRate.Value) && Math.Abs(lowRate.Value - highRate.Value) < .006, "preset is stable across refresh rates: " + config);
            for (int i = 0; i < 120; i++) lowRate.Step(1d / 60, config);
            Check(!lowRate.Active && lowRate.Value == 1, "preset settles: " + config);
        }
        var closing = new Spring(1) { Target = 0 }; double minimum = 1;
        for (int i = 0; i < 120; i++) { closing.Step(1d / 60, Springs.Close); minimum = Math.Min(minimum, closing.Value); }
        Check(!closing.Active && minimum >= -.0002, "close settles without visible pill undershoot");
        Check(Spring.RubberBand(0) == 0 && Spring.RubberBand(1e9, 80) < 80 && Spring.RubberBand(100, 80) > 0, "rubber band respects a custom limit");
        Check(Ease.Smoothstep(.2, .8, -1) == 0 && Ease.Smoothstep(.2, .8, 2) == 1 && Math.Abs(Ease.Smoothstep(.2, .8, .5) - .5) < .0001, "smoothstep clamps and blends symmetrically");
        var spectrum = new Spectrum(48000); for (int i = 0; i < 1024; i++) spectrum.Push(0); Check(spectrum.Bands.All(b => b == 0), "silent output gives zero spectrum");
        for (int i = 0; i < 1024; i++) spectrum.Push(.2 * Math.Sin(2 * Math.PI * 1000 * i / 48000)); Check(Array.IndexOf(spectrum.Bands, spectrum.Bands.Max()) == 4 && spectrum.Bands[4] > .5, "1 kHz input produces the correct frequency band");
        Check(LoopbackVisualizer.Decode(new byte[] { 0, 0, 128 }, 0, 24, false) == -1 && LoopbackVisualizer.Decode(new byte[] { 0, 128 }, 0, 16, false) == -1, "signed PCM decoding");
        var fixture = PreviewArtwork(); var color = ArtworkPalette.Extract(fixture); Check(color.R > color.B && color.R > color.G, "artwork palette extracts the warm dominant color");
        var window = new IslandWindow(); App.Island = window; Check(window.Width > 0 && window.MediaTab != null, "island XAML loads");
        window.UpdatePlaybackVisuals(true); string pauseGeometry = window.PlayGlyph.Data.ToString(); Check(System.Windows.Automation.AutomationProperties.GetName(window.Play) == "Pause", "playing state exposes Pause action");
        window.UpdatePlaybackVisuals(false); Check(System.Windows.Automation.AutomationProperties.GetName(window.Play) == "Play" && window.PlayGlyph.Data.ToString() != pauseGeometry, "play and pause have distinct visible geometry");
        Check(WorkspaceData.Calculate("2*(3+4)") == "14", "calculator precedence");
        bool badMath = false; try { WorkspaceData.Calculate("Process.Start('x')"); } catch (ArgumentException) { badMath = true; } Check(badMath, "calculator rejects non-arithmetic input");
        Check(WorkspaceData.SmartAction("https://example.com") == "Open link" && WorkspaceData.SmartAction("javascript:alert(1)") == "Copy", "clipboard smart actions restrict executable URI schemes");
        var lrc = Lyrics.Parse("[00:01.00]One\n[00:02.50][00:05.00]Two"); Check(lrc.Count == 3 && Lyrics.At(lrc, 2.6) == "Two", "multiple lyric timestamps and current line");
        bool invalidActivity = false; try { LocalActivityServer.Validate(new LiveActivity { Id = "x", Title = "a", Progress = double.NaN }); } catch (ArgumentException) { invalidActivity = true; } Check(invalidActivity, "developer API rejects non-finite progress");
        invalidActivity = false; try { LocalActivityServer.Validate(new LiveActivity { Id = "x", Title = "a", Provider = null! }); } catch (ArgumentException) { invalidActivity = true; } Check(invalidActivity, "developer API validates null provider");
        LocalActivityServer.Validate(new LiveActivity { Id = "build-1", Title = "Build", Kind = "build", Progress = .5 }); Check(true, "developer API accepts a bounded activity");
        string scratch = Path.Combine(AppContext.BaseDirectory, "self-test-work-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
        try
        {
            string file = Path.Combine(scratch, "example.txt"); File.WriteAllText(file, "round trip"); string archive = Path.Combine(scratch, "safe.zip"); WorkspaceData.ZipFiles(new[] { file }, archive); string folder = Path.Combine(scratch, "extracted"); WorkspaceData.Extract(archive, folder); Check(File.ReadAllText(Path.Combine(folder, "example.txt")) == "round trip", "shelf ZIP round trip");
            string unsafeZip = Path.Combine(scratch, "unsafe.zip"); using (var zip = System.IO.Compression.ZipFile.Open(unsafeZip, System.IO.Compression.ZipArchiveMode.Create)) zip.CreateEntry("../escape.txt");
            bool unsafeRejected = false; try { WorkspaceData.Extract(unsafeZip, Path.Combine(scratch, "blocked")); } catch (IOException) { unsafeRejected = true; } Check(unsafeRejected && !File.Exists(Path.Combine(scratch, "escape.txt")), "ZIP traversal rejected before extraction");
            string image = Path.Combine(scratch, "test.png"); CaptureTools.SaveImage(fixture, image, "PNG"); using (var png = File.OpenRead(image)) { var decoded = System.Windows.Media.Imaging.BitmapDecoder.Create(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); Check(decoded.Frames[0].PixelWidth == 128, "image converter produces readable PNG"); }
            string pdf = Path.Combine(scratch, "test.pdf"); CaptureTools.SavePdf(fixture, pdf); var pdfBytes = File.ReadAllBytes(pdf); Check(System.Text.Encoding.ASCII.GetString(pdfBytes.Take(8).ToArray()).StartsWith("%PDF-1.4"), "image PDF export has a valid header");
            string log = Path.Combine(scratch, "usage.jsonl"); File.WriteAllText(log, "{\"tokens\":123,\"costUsd\":0.01}\n"); Check(UsageReader.Read(log).Contains("123"), "normalized AI usage log read");
        }
        finally { if (Path.GetFullPath(scratch).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase)) Directory.Delete(scratch, true); }
        App.Workspace = new WorkspaceServices(); var tools = new WorkspaceView();
        foreach (string page in WorkspaceView.Sections) { tools.Select(page); Check(tools.Body.Children.Count > 0, page + " workspace loads"); }
        string notes = WorkspaceData.Current.Notes; WorkspaceData.Current.Notes = "A few things for today.\n\nMake space for focused work.\nKeep the essentials close.";
        tools.Select("Home"); Render((FrameworkElement)tools.Content, 1040, 740, "workspace.png"); WorkspaceData.Current.Notes = notes; tools.Dispose();
        int windowCount = Application.Current.Windows.Count;
        foreach (string page in WorkspaceView.Sections)
        {
            window.OpenWorkspace(page); window.SettlePreview();
            Check(window.WorkspaceOpen && ReferenceEquals(window.WorkspaceHost.Children[0], window.Workspace) && window.Workspace!.PageTitle.Text == page && Application.Current.Windows.Count == windowCount, page + " opens inside island without another window");
        }
        window.OpenWorkspace("Commands"); window.SettlePreview();
        Check(window.Workspace!.PageTitle.Text == "Command palette", "shortcut opens command palette inside an already-open island");
        window.Workspace!.CommandSearch.Text = "a query";
        window.Workspace.CommandSearch.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(window), Environment.TickCount, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
        Check(window.WorkspaceOpen && window.Workspace.CommandSearch.Text.Length == 0, "Escape clears a command query without closing the workspace");
        WorkspaceData.Current.Notes = "Preview fixture · quick notes\n\nMake space for focused work.\nKeep the essentials close.";
        window.OpenWorkspace("Notes"); window.SettlePreview();
        Check(window.WorkspaceSurface.IsHitTestVisible && !window.Expanded.IsHitTestVisible, "workspace receives input without the media view underneath");
        Render(window.Frame, 1000, 820, "island-notes.png");
        window.CloseWorkspace(); window.SettlePreview();
        Check(!window.WorkspaceOpen && window.WorkspaceSurface.Opacity == 0 && window.Expanded.IsHitTestVisible, "back restores interactive media view");
        window.OpenWorkspace("Home"); window.SettlePreview(); Render(window.Frame, 1000, 820, "island-workspace.png");
        window.Collapse(); window.SettlePreview();
        WorkspaceData.Current.Notes = notes;
        Check(!window.WorkspaceOpen && !window.WorkspaceSurface.IsHitTestVisible && window.WorkspaceSurface.Opacity == 0 && window.ShellScale.ScaleX < .6, "collapse closes embedded tools and returns to pill geometry");
        foreach (var icon in Icons.Paths) Check(!Geometry.Parse(icon.Value).Bounds.IsEmpty, icon.Key + " vector geometry is valid");
        Check(Icons.KindFor("Previous track") == "previous" && Icons.KindFor("Pause") == "pause" && Icons.KindFor("Notes") == "note", "semantic icons cover navigation and playback states");
        var namedButton = new System.Windows.Controls.Button { Content = Icons.Label("Save note") }; Icons.Enhance(namedButton);
        Check(System.Windows.Automation.AutomationProperties.GetName(namedButton) == "Save note", "compound icon buttons retain accessible text");
        var iconButton = new System.Windows.Controls.Button(); Icons.SetLabel(iconButton, "Play"); Icons.SetLabel(iconButton, "Pause");
        Check(iconButton.Content is System.Windows.Controls.StackPanel iconRow && ((IconView)iconRow.Children[0]).Kind == "pause" && System.Windows.Automation.AutomationProperties.GetName(iconButton) == "Pause", "state changes preserve vector icon and accessibility");
        var gallery = new System.Windows.Controls.WrapPanel { Width = 880, Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)) };
        foreach (string kind in Icons.Paths.Keys) { var item = new System.Windows.Controls.Button { Width = 110, Height = 76, Content = new System.Windows.Controls.StackPanel { Children = { new IconView { Kind = kind, Width = 24, Height = 24, Margin = new Thickness(0, 0, 0, 7) }, new System.Windows.Controls.TextBlock { Text = kind, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center } } } }; gallery.Children.Add(item); }
        Render(gallery, 880, 532, "icons.png");
        var settings = new SettingsWindow(); Check(settings.Width >= 850, "settings XAML loads");
        foreach (string section in new[] { "General", "Battery", "Connectivity", "Focus", "Display", "Sound", "Now Playing", "Calendar & Weather", "Lock Screen", "About" }) { settings.Select(section); Check(settings.ContentPanel.Children.Count > 0, section + " settings render"); }
        settings.Select("Sound"); Render((FrameworkElement)settings.Content, 1020, 730, "settings.png");
        window.Artwork.Source = window.ArtBackdrop.Source = fixture; window.ArtBackdrop.Opacity = .6; window.TrackTitle.Text = "After the last light"; window.TrackArtist.Text = "Island Sessions · preview fixture"; window.ApplyPreferences();
        var originalArt = window.Artwork; double originalWidth = window.Width;
        window.UpdatePlaybackVisuals(true); window.Expand(); window.SettlePreview();
        Check(window.Width == originalWidth && ReferenceEquals(originalArt, window.Artwork), "expand preserves window size and the shared artwork instance");
        Render(window.Frame, 1000, 430, "now-playing.png");
        window.TodayTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); window.SettlePreview(); Render(window.Frame, 1000, 430, "today.png");
        window.ActivityTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); window.SettlePreview(); Render(window.Frame, 1000, 430, "activities.png");
        window.MediaTab!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        window.MediaTab.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(window), Environment.TickCount, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent }); window.SettlePreview();
        Check(window.Expanded.Opacity == 0, "Escape collapses media while a tab button has focus");
        window.Collapse(); window.SettlePreview(); Render(window.Frame, 1000, 90, "collapsed.png");
        Check(window.Expanded.Opacity == 0 && window.ShellScale.ScaleX < .6, "collapse hides expanded content without relayout");
        window.TimerToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Check(System.Windows.Automation.AutomationProperties.GetName(window.TimerToggle) == "Pause", "timer starts"); window.TimerToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Check(System.Windows.Automation.AutomationProperties.GetName(window.TimerToggle) == "Resume", "timer pauses");
        window.Dispose(); window.Close(); settings.Close();
        var audio = Audio.Read(); Check(!audio.HasValue || (audio.Value.Level >= 0 && audio.Value.Level <= 100), "Core Audio read bounds");
        var report = Path.Combine(AppContext.BaseDirectory, "self-test.txt"); File.WriteAllText(report, $"PASS · {count} checks · {DateTimeOffset.Now:u}\nSettings migration/validation, spring stability/retargeting, spectrum/PCM analysis, artwork palette, fixed window/shared visual invariants, widget/settings renders, embedded workspace/navigation/lifecycle, vector geometry/accessibility and timer controls.\n");
    }
    private static BitmapSource PreviewArtwork()
    {
        const int size = 128; byte[] pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            int offset = (y * size + x) * 4; double distance = Math.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64));
            bool ring = distance > 24 && distance < 54; pixels[offset] = (byte)(ring ? 35 : 60 + y / 4); pixels[offset + 1] = (byte)(ring ? 40 : 40 + x / 5); pixels[offset + 2] = (byte)(ring ? 95 : 190 + x / 3); pixels[offset + 3] = 255;
        }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4); bitmap.Freeze(); return bitmap;
    }
    internal static async Task Diagnostics()
    {
        var services = new DesktopServices(); await services.Initialize(); await Task.Run(services.ReadBrightness);
        string media, devices;
        try { var m = await services.ReadMedia(); media = m.Title + " · " + m.Artist; } catch (Exception ex) { media = ex.Message; }
        try { await services.ReadDevices(); devices = services.DeviceSummary; } catch (Exception ex) { devices = ex.Message; }
        Native.GetSystemPowerStatus(out var power);
        using var loopback = new LoopbackVisualizer(); loopback.SetEnabled(true); double peak = 0;
        for (int i = 0; i < 40; i++) { await Task.Delay(50); for (int band = 0; band < 8; band++) peak = Math.Max(peak, loopback.Level(band)); }
        string mixer; try { var sessions = AppMixer.Enumerate(); mixer = sessions.Count + " sessions enumerated"; foreach (var session in sessions) session.Dispose(); } catch (Exception ex) { mixer = ex.Message; }
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "diagnostics.txt"), $"Island Windows integration diagnostics · {DateTimeOffset.Now:u}\nCore Audio: {Audio.Read()}\nMedia: {media}\nPlaying state: {services.IsPlaying}\nMixer: {mixer}\nBrightness: {services.BrightnessStatus} ({services.Brightness})\nBluetooth: {devices}\nBattery: {power.Percent}% / AC={power.AC}\nPresentation: {Native.PresentationActive()}\nLoopback: {loopback.Status}; frames={loopback.FramesCaptured}; peak band={peak:0.000}\nNo system settings changed; no notification permission requested. Audio samples were analyzed only in memory.\n");
    }
    internal static async Task Api()
    {
        int checks = 0; void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); checks++; }
        using var server = new LocalActivityServer(); server.Start(0); if (server.BoundPort == 0) throw new IOException(server.Status);
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { BaseAddress = new Uri($"http://127.0.0.1:{server.BoundPort}"), Timeout = TimeSpan.FromSeconds(5) };
        using (var response = await client.GetAsync("/activities")) Check(response.StatusCode == HttpStatusCode.Unauthorized, "Unauthenticated API access must be rejected");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.Token);
        using (var response = await client.PostAsync("/activities", new StringContent("{broken", Encoding.UTF8, "application/json"))) Check(response.StatusCode == HttpStatusCode.BadRequest, "Malformed JSON must be rejected");
        using (var response = await client.PostAsync("/activities", new StringContent("{\"id\":\"test-build\",\"title\":\"Test build\",\"kind\":\"build\",\"progress\":0.5}", Encoding.UTF8, "application/json"))) Check(response.IsSuccessStatusCode, "Authenticated activity should succeed");
        using (var response = await client.GetAsync("/activities")) Check((await response.Content.ReadAsStringAsync()).Contains("test-build"), "Authenticated GET should return the posted activity");
        const string approval = "{\"id\":\"approval\",\"title\":\"Test approval\",\"kind\":\"approval\",\"decision\":\"allow\"}";
        using (var response = await client.PostAsync("/activities", new StringContent(approval, Encoding.UTF8, "application/json"))) Check(response.IsSuccessStatusCode && server.Activities["approval"].Decision == null, "Clients must not grant their own approval");
        server.Activities["approval"].Decision = "deny";
        using (var response = await client.PostAsync("/activities", new StringContent(approval, Encoding.UTF8, "application/json"))) Check(response.IsSuccessStatusCode && server.Activities["approval"].Decision == "deny", "A client update must preserve the user's decision");
        bool oversizedRejected;
        try { using var response = await client.PostAsync("/activities", new StringContent(new string('x', 70000), Encoding.UTF8, "application/json")); oversizedRejected = response.StatusCode == HttpStatusCode.BadRequest; }
        catch (HttpRequestException) { oversizedRejected = true; }
        Check(oversizedRejected && server.Activities.Count == 2, "Oversized request must be rejected without adding an activity");
        using (var response = await client.GetAsync("/activities")) Check(response.IsSuccessStatusCode, "Listener must remain usable after an oversized request");
        server.Stop(); Check(server.BoundPort == 0, "Stopping must release the listener");
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "api-self-test.txt"), $"PASS · {checks} HTTP integration checks · {DateTimeOffset.Now:u}\nAuthentication, malformed JSON, activity round trip, approval trust boundaries, request-size limit and listener shutdown.\n");
    }
    internal static async Task Performance()
    {
        App.Prefs.DebugFps = false; App.Prefs.ReplaceVolumeKeys = false; App.Prefs.HoverExpand = false; App.Prefs.Focus = false; App.Prefs.Weather = false; App.Prefs.Notifications = false;
        App.Prefs.ClipboardHistory = App.Prefs.DownloadAlerts = App.Prefs.DeveloperApi = false;
        App.Workspace = new WorkspaceServices();
        App.Prefs.AnimationStyle = "Spring"; App.Prefs.ReducedMotion = false;
        using var process = Process.GetCurrentProcess(); var window = new IslandWindow(); App.Island = window; window.Show(); window.OpenWorkspace("Notes"); window.Collapse();
        await Task.Delay(4000); window.SettlePreview();
        var cpu = process.TotalProcessorTime; var time = Stopwatch.StartNew(); await Task.Delay(20000); time.Stop();
        process.Refresh(); double oneCore = (process.TotalProcessorTime - cpu).TotalSeconds / time.Elapsed.TotalSeconds * 100;
        var intervals = new List<double>(); TimeSpan previous = TimeSpan.MinValue; int layouts = 0, sizes = 0;
        EventHandler rendering = (_, args) => { var current = ((RenderingEventArgs)args).RenderingTime; if (previous != TimeSpan.MinValue && current > previous) intervals.Add((current - previous).TotalMilliseconds); previous = current; };
        EventHandler layout = (_, _) => layouts++; SizeChangedEventHandler size = (_, _) => sizes++;
        CompositionTarget.Rendering += rendering; window.Stage.LayoutUpdated += layout; window.SizeChanged += size;
        for (int i = 0; i < 5; i++) { if (i >= 3) window.OpenWorkspace("Notes"); else window.Expand(); await Task.Delay(700); window.Collapse(); await Task.Delay(700); }
        CompositionTarget.Rendering -= rendering; window.Stage.LayoutUpdated -= layout; window.SizeChanged -= size;
        window.Dispose(); window.Close();
        var ordered = intervals.OrderBy(x => x).ToArray(); double average = intervals.Count == 0 ? 0 : intervals.Average(), p95 = ordered.Length == 0 ? 0 : ordered[(int)((ordered.Length - 1) * .95)];
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "performance.txt"), $"Island 1.3 · local performance sample · {DateTimeOffset.Now:u}\n20-second collapsed idle (debug off, no loopback): {oneCore / Environment.ProcessorCount:0.000}% total CPU; {oneCore:0.000}% of one logical core; {Environment.ProcessorCount} logical CPUs.\nWorking set: {process.WorkingSet64 / 1048576d:0.0} MB.\nThree media and two embedded-workspace expand/collapse cycles: {intervals.Count} render intervals; callback average {(average > 0 ? 1000 / average : 0):0.0} FPS; p95 {p95:0.00} ms; worst {(ordered.Length > 0 ? ordered[^1] : 0):0.00} ms.\nWindow size changes during transitions: {sizes}; Stage layout notifications including media/service updates: {layouts}.\nGraphics tier: {RenderCapability.Tier >> 16}; render mode: {System.Windows.Media.RenderOptions.ProcessRenderMode}.\nRendering callbacks are a diagnostic proxy, not GPU presentation timestamps. This sample is not a cross-hardware 60 FPS guarantee.\n");
    }
    private static void Render(FrameworkElement element, int width, int height, string name)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        Icons.Apply(element); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var directory = Path.Combine(AppContext.BaseDirectory, "previews"); Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name)); encoder.Save(file);
    }
}
