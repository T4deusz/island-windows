using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Island;
public partial class IslandWindow : Window, IDisposable
{
    internal readonly DesktopServices Services = new();
    private readonly LoopbackVisualizer loopback = new();
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer audioClock = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer collapseClock = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer peekClock = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer hudClock = new();
    private readonly Spring workspaceSpring = new();
    private WorkspaceView? workspace;
    private bool workspaceOpen;
    internal bool WorkspaceOpen => workspaceOpen;
    internal WorkspaceView? Workspace => workspace;
    private readonly Spring shape = new(), hudSpring = new(), levelSpring = new(), inlineSpring = new(), swipeSpring = new();
    private readonly Spring[] pageSprings = { new(1), new(), new() };
    private readonly Spring[] entries = { new(), new(), new() };
    private readonly double[] barLevels = new double[8];
    private readonly ToolTip seekTip = new() { Placement = PlacementMode.Top };
    private bool expanded, busy, audioBusy, locked, ready, disposed, rendering, scrubbing, dragging, pointerDown;
    private bool inlineHud, hotkeyRegistered;
    private long audioRevision;
    private int page, ticks, timerSeconds = 25 * 60, frameCount;
    private double lastFrame, fpsStart, fpsTotal, fpsWorst, entryStart;
    private double seekDuration;
    private TimeSpan lastRenderTime = TimeSpan.MinValue;
    private string seekKey = "", lastTrack = "";
    private DateTimeOffset? timerEnd;
    private int focusMinutes = 25;
    private bool breakSession;
    private Point down;
    private (int Level, bool Muted)? lastAudio;
    private int? lastBrightness;
    private byte? lastAc, lastBattery;
    private IntPtr handle;
    private HwndSource? source;
    private VolumeKeyHook? volumeKeys;
    private System.Windows.Media.Imaging.BitmapSource? lastArt;
    private FrameworkElement[] pages = null!, entranceElements = null!;
    internal string HotkeyStatus => hotkeyRegistered ? "Ctrl + Alt + Space" : "Shortcut unavailable · already used by another app";
    internal string VisualizerStatus => loopback.Status;
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    private bool Instant => App.Prefs.ReducedMotion || App.Prefs.AnimationStyle == "Instant" || !SystemParameters.ClientAreaAnimation;
    public IslandWindow()
    {
        InitializeComponent();
        Services.PlaybackChanged += PlaybackUpdated;
        SettingsButton.Content = new IconView { Kind = "settings" }; CollapseButton.Content = new IconView { Kind = "up" };
        WorkspaceSurface.PreviewMouseLeftButtonDown += (_, _) => Activate();
        AllowDrop = true; DragOver += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; Expand(); } };
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) { WorkspaceData.Current.AddFiles(paths); App.OpenTools("Shelf"); } };
        pages = new FrameworkElement[] { MediaPage, TodayPage, ActivityPage };
        entranceElements = new FrameworkElement[] { TrackArtist, ProgressGroup, ControlsGroup };
        foreach (var element in pages.Concat(entranceElements)) element.RenderTransform = MakeTransform();
        for (int i = 0; i < 8; i++)
        {
            var bar = new Border { Width = 3, Height = 36, Margin = new Thickness(1.5, 0, 1.5, 0), CornerRadius = new CornerRadius(1.5), RenderTransformOrigin = new Point(.5, 1), RenderTransform = new ScaleTransform(1, .06) };
            bar.SetResourceReference(Border.BackgroundProperty, "MediaAccent"); Visualizer.Children.Add(bar);
        }
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
            hotkeyRegistered = Native.RegisterHotKey(handle, 1, 0x4003, 0x20);
            Native.RegisterHotKey(handle, 2, 0x4003, 0x4B);
            DesktopNative.AddClipboardFormatListener(handle);
            Native.SetWindowLong(handle, -20, Native.GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            volumeKeys = new VolumeKeyHook(handle, () => App.Prefs.ReplaceVolumeKeys && !locked && IsVisible && lastAudio.HasValue);
            ApplyPreferences();
        };
        Loaded += async (_, _) => { await Services.Initialize(); ready = true; await Poll(); };
        Compact.MouseEnter += (_, _) => { collapseClock.Stop(); if (App.Prefs.HoverExpand && !expanded) peekClock.Start(); };
        MouseLeave += (_, _) => { peekClock.Stop(); if (!expanded || App.Prefs.AutoHide && !workspaceOpen) collapseClock.Start(); };
        MouseEnter += (_, _) => collapseClock.Stop();
        PreviewMouseLeftButtonDown += PointerDown;
        PreviewMouseMove += PointerMove;
        PreviewMouseLeftButtonUp += PointerUp;
        LostMouseCapture += (_, _) => { dragging = pointerDown = false; swipeSpring.Target = 0; Wake(); };
        MouseRightButtonUp += (_, _) => { if (!workspaceOpen) App.OpenSettings(); };
        MouseWheel += (_, e) => { if (workspaceOpen) return; AdjustVolume(Math.Sign(e.Delta) * 2, true); e.Handled = true; };
        KeyDown += (_, e) =>
        {
            if (workspaceOpen) { if (e.Key == Key.Escape && !string.IsNullOrEmpty(workspace?.CommandSearch.Text)) return; if (e.Key == Key.Escape) { CloseWorkspace(); e.Handled = true; } return; }
            if (e.OriginalSource is Slider) return;
            if (e.Key == Key.Escape) Collapse();
            if (e.Key == Key.Left) Navigate(-1);
            if (e.Key == Key.Right) Navigate(1);
        };
        collapseClock.Tick += (_, _) => { collapseClock.Stop(); if (!IsMouseOver && !(workspaceOpen && IsKeyboardFocusWithin)) Collapse(); };
        peekClock.Tick += (_, _) => { peekClock.Stop(); if (!expanded && Compact.IsMouseOver) { shape.Target = .16; Wake(); } };
        hudClock.Tick += (_, _) => { hudClock.Stop(); hudSpring.Target = inlineSpring.Target = 0; Wake(); };
        clock.Tick += async (_, _) => { TickTimer(); await Poll(); }; clock.Start();
        audioClock.Tick += async (_, _) => await PollAudio(); audioClock.Start();
        TrackProgress.PreviewMouseLeftButtonDown += (_, _) => BeginScrub();
        TrackProgress.ValueChanged += (_, _) => { if (scrubbing) { seekTip.Content = FormatTime(TrackProgress.Value * seekDuration); seekTip.IsOpen = true; } };
        TrackProgress.PreviewMouseLeftButtonUp += async (_, _) => await CommitScrub();
        TrackProgress.PreviewKeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) BeginScrub(); };
        TrackProgress.PreviewKeyUp += async (_, _) => { if (scrubbing) await CommitScrub(); };
        TrackProgress.LostKeyboardFocus += async (_, _) => { if (scrubbing) await CommitScrub(); };
        seekTip.PlacementTarget = TrackProgress;
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        IsVisibleChanged += (_, _) => { UpdateCapture(); if (!IsVisible) { CloseWorkspace(); StopRendering(); } else Wake(); };
        BuildDots(); ShowPage(0); BuildCalendar(); Icons.Apply(this); RenderState(0);
    }
    private static TransformGroup MakeTransform()
    {
        var group = new TransformGroup(); group.Children.Add(new ScaleTransform(1, 1)); group.Children.Add(new TranslateTransform()); return group;
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == VolumeKeyHook.Message)
        {
            int key = wParam.ToInt32();
            try { if (key == 0xAD) { audioRevision++; Audio.ToggleMute(); _ = PollAudio(); } else AdjustVolume(key == 0xAF ? 2 : -2, false); }
            catch (Exception ex) { Log.Write(ex); }
            handled = true;
        }
        if (msg == 0x312 && wParam.ToInt32() == 1) { if (!locked) { Show(); if (expanded) Collapse(); else { Expand(); Activate(); MediaTab.Focus(); } } handled = true; }
        if (msg == 0x312 && wParam.ToInt32() == 2 && !locked) { App.OpenTools("Commands"); handled = true; }
        if (msg == 0x31D && App.Prefs.ClipboardHistory && !locked) WorkspaceData.Current.CaptureClipboard();
        if (msg == 0x02E0) Dispatcher.BeginInvoke(() => Place());
        if (msg == 0x84)
        {
            var cursor = Native.CursorPosition(); var p = Stage.PointFromScreen(cursor);
            double width = PanelWidth, height = PanelHeight;
            bool onIsland = new Rect(360 - width / 2, 12, width, height).Contains(p);
            bool onHud = hudSpring.Value > .02 && new Rect(180, 12 + height + 12, 360, 72).Contains(p);
            if (!onIsland && !onHud) { handled = true; return new IntPtr(-1); }
        }
        return IntPtr.Zero;
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (workspaceOpen || IsInteractive(e.OriginalSource as DependencyObject)) return;
        down = e.GetPosition(Stage); pointerDown = true; dragging = false; Stage.CaptureMouse();
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!pointerDown || e.LeftButton != MouseButtonState.Pressed || !expanded || !App.Prefs.HorizontalGestures) return;
        var delta = e.GetPosition(Stage) - down;
        if (Math.Abs(delta.X) < 8 && !dragging) return;
        dragging = true; Stage.CaptureMouse();
        var order = App.Prefs.WidgetOrder; int index = order.IndexOf(page);
        bool edge = index == 0 && delta.X > 0 || index == order.Count - 1 && delta.X < 0;
        swipeSpring.Snap(edge ? Spring.RubberBand(delta.X) : delta.X * .55); Wake();
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!pointerDown) return;
        var delta = e.GetPosition(Stage) - down; bool wasDragging = dragging; pointerDown = dragging = false;
        Stage.ReleaseMouseCapture();
        if (App.Prefs.HorizontalGestures && Math.Abs(delta.X) > 65 && Math.Abs(delta.X) > Math.Abs(delta.Y)) { Navigate(delta.X < 0 ? 1 : -1); Expand(); }
        else if (App.Prefs.VerticalGestures && delta.Y > 45) Expand();
        else if (App.Prefs.VerticalGestures && delta.Y < -45) Collapse();
        else if (!wasDragging && delta.Length < 8) { if (expanded) Collapse(); else Expand(); }
        swipeSpring.Target = 0; Wake();
    }
    private static bool IsInteractive(DependencyObject? node)
    {
        while (node != null) { if (node is ButtonBase or Slider or Thumb or TextBoxBase or System.Windows.Controls.Primitives.Selector or ScrollViewer) return true; node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node); } return false;
    }
    private void Navigate(int direction)
    {
        var order = App.Prefs.WidgetOrder; int index = order.IndexOf(page), next = Math.Clamp(index + direction, 0, order.Count - 1);
        if (next == index) { swipeSpring.Snap(direction * -24); swipeSpring.Target = 0; Wake(); } else ShowPage(order[next]);
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => ApplyPreferences());
    private bool visibleBeforeLock;
    private void SessionSwitch(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (e.Reason == SessionSwitchReason.SessionLock) { locked = true; visibleBeforeLock = IsVisible; volumeKeys?.Reset(); Hide(); UpdateCapture(); }
        else if (e.Reason == SessionSwitchReason.SessionUnlock) { locked = false; if (visibleBeforeLock) Show(); UpdateCapture(); }
    });
    internal void ApplyPreferences()
    {
        var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(App.Prefs.Accent)); accent.Freeze();
        Application.Current.Resources["Accent"] = accent; UpdatePalette(); BuildDots();
        FpsPanel.Visibility = App.Prefs.DebugFps ? Visibility.Visible : Visibility.Collapsed;
        Shell.CornerRadius = PillSurface.CornerRadius = App.Prefs.NotchMode ? new CornerRadius(0, 0, 27, 27) : new CornerRadius(27);
        if (handle != IntPtr.Zero) DesktopNative.SetWindowDisplayAffinity(handle, App.Prefs.HideFromCapture ? 0x11u : 0u);
        foreach (Window window in Application.Current.Windows) if (new WindowInteropHelper(window).Handle != IntPtr.Zero) DesktopNative.ApplyCapturePrivacy(window);
        if (handle != IntPtr.Zero) { double scale = App.Prefs.Scale; var screen = Forms.Screen.AllScreens[Math.Clamp(App.Prefs.Monitor, 0, Forms.Screen.AllScreens.Length - 1)]; var dpi = VisualTreeHelper.GetDpi(this); scale = Math.Min(scale, Math.Min(screen.WorkingArea.Width / dpi.DpiScaleX / 1000, (screen.WorkingArea.Height / dpi.DpiScaleY - App.Prefs.Offset) / 820)); Width = 1000 * Math.Max(.45, scale); Height = 820 * Math.Max(.45, scale); Place(); }
        UpdateCapture(); Wake();
    }
    private void Place()
    {
        var screens = Forms.Screen.AllScreens; var screen = screens[Math.Clamp(App.Prefs.Monitor, 0, screens.Length - 1)];
        var dpi = VisualTreeHelper.GetDpi(this); var bounds = screen.Bounds;
        Left = (bounds.Left + bounds.Width / 2d) / dpi.DpiScaleX - Width / 2;
        Top = bounds.Top / dpi.DpiScaleY + (App.Prefs.NotchMode ? -12 : App.Prefs.Offset);
    }
    internal void Expand()
    {
        if (locked || expanded) return; peekClock.Stop(); expanded = true; shape.Target = 1; entryStart = Now;
        if (inlineSpring.Target > 0) ShowHud(InlineIcon.Text == "\uE74F" ? "⊘" : "◖", InlineIcon.Text == "\uE74F" ? "Muted" : "Volume", "System output", (int)Math.Round(levelSpring.Target * 100), true);
        foreach (var entry in entries) entry.Target = 0;
        UpdateCapture(); Wake();
    }
    internal void Collapse()
    {
        CloseWorkspace(); expanded = false; peekClock.Stop(); shape.Target = 0; foreach (var entry in entries) entry.Target = 0;
        if (App.Prefs.HudPosition == "Inline when collapsed" && hudSpring.Target > 0 && HudProgress.Visibility == Visibility.Visible && HudTitle.Text is "Volume" or "Muted") ShowLevelHud(HudTitle.Text, (int)Math.Round(levelSpring.Target * 100), true);
        UpdateCapture(); Wake();
    }
    private void ShowPage(int index)
    {
        int previous = page; page = index;
        for (int i = 0; i < 3; i++) { pageSprings[i].Target = i == page ? 1 : 0; pages[i].IsHitTestVisible = i == page; }
        var buttons = new[] { MediaTab, TodayTab, ActivityTab };
        for (int i = 0; i < buttons.Length; i++) { buttons[i].Foreground = i == index ? Brushes.White : Brushes.Gray; buttons[i].Background = new SolidColorBrush(i == index ? Color.FromArgb(24, 255, 255, 255) : Colors.Transparent); }
        if (previous != page) entryStart = Now;
        if (index == 1) BuildCalendar();
        BuildDots(); UpdateCapture(); Wake();
    }
    private void BuildDots()
    {
        PageDots.Children.Clear();
        foreach (int index in App.Prefs.WidgetOrder)
        {
            var button = new Button { Width = 16, Height = 14, Padding = new Thickness(4), Background = Brushes.Transparent, ToolTip = new[] { "Now playing", "Today", "Activities" }[index] };
            button.Content = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = index == page ? Brushes.White : new SolidColorBrush(Color.FromRgb(76, 76, 88)) };
            System.Windows.Automation.AutomationProperties.SetName(button, "Show " + button.ToolTip);
            button.Click += (_, _) => ShowPage(index); PageDots.Children.Add(button);
        }
        var tabPanel = MediaTab.Parent as Panel;
        if (tabPanel != null) { tabPanel.Children.Clear(); foreach (int index in App.Prefs.WidgetOrder) tabPanel.Children.Add(new[] { MediaTab, TodayTab, ActivityTab }[index]); }
    }
    private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
    private void Wake()
    {
        if (disposed || rendering || !IsVisible && handle != IntPtr.Zero) return;
        if (Instant) { SnapMotion(); RenderState(0); if (!CaptureWanted && !App.Prefs.DebugFps) return; }
        rendering = true; lastFrame = fpsStart = Now; lastRenderTime = TimeSpan.MinValue; frameCount = 0; fpsTotal = fpsWorst = 0; CompositionTarget.Rendering += OnFrame;
    }
    private void StopRendering() { if (!rendering) return; CompositionTarget.Rendering -= OnFrame; rendering = false; }
    private bool MotionActive => workspaceSpring.Active || shape.Active || hudSpring.Active || inlineSpring.Active || levelSpring.Active || swipeSpring.Active || pageSprings.Any(s => s.Active) || entries.Any(s => s.Active) || expanded && entries.Any(s => s.Target == 0);
    private void SnapMotion()
    {
        workspaceSpring.Snap(workspaceSpring.Target); shape.Snap(shape.Target); hudSpring.Snap(hudSpring.Target); inlineSpring.Snap(inlineSpring.Target); levelSpring.Snap(levelSpring.Target); swipeSpring.Snap(0);
        foreach (var spring in pageSprings) spring.Snap(spring.Target);
        foreach (var entry in entries) entry.Snap(expanded ? 1 : 0);
    }
    private void OnFrame(object? sender, EventArgs e)
    {
        var renderTime = ((RenderingEventArgs)e).RenderingTime; if (renderTime == lastRenderTime) return; lastRenderTime = renderTime;
        double now = Now, dt = Math.Clamp(now - lastFrame, 0, .1); lastFrame = now;
        if (Instant) SnapMotion();
        else
        {
            SpringConfig Style(SpringConfig config) => App.Prefs.AnimationStyle == "Gentle" ? config with { Damping = Math.Max(1, config.Damping) } : config;
            workspaceSpring.Step(dt, Style(workspaceSpring.Target > 0 ? Springs.Open : Springs.Close));
            shape.Step(dt, Style(shape.Target == 1 ? Springs.Open : shape.Target > 0 ? Springs.Snappy : Springs.Close));
            hudSpring.Step(dt, Style(hudSpring.Target > 0 ? Springs.Bouncy : Springs.Close));
            inlineSpring.Step(dt, Style(inlineSpring.Target > 0 ? Springs.Snappy : Springs.Close));
            levelSpring.Step(dt, Springs.Tracking); swipeSpring.Step(dt, Style(Springs.Release));
            foreach (var spring in pageSprings) spring.Step(dt, Style(Springs.Smooth));
            for (int i = 0; i < entries.Length; i++) { entries[i].Target = expanded && now - entryStart >= i * .03 ? 1 : 0; entries[i].Step(dt, Style(Springs.Smooth)); }
        }
        RenderState(dt);
        frameCount++; fpsTotal += dt; fpsWorst = Math.Max(fpsWorst, dt);
        if (App.Prefs.DebugFps && now - fpsStart >= .5)
        {
            FpsText.Text = $"{frameCount / Math.Max(.001, fpsTotal):0} FPS · worst {fpsWorst * 1000:0.0} ms · GPU tier {RenderCapability.Tier >> 16}\n{loopback.Status}";
            fpsStart = now; frameCount = 0; fpsTotal = fpsWorst = 0;
        }
        if (!MotionActive && !CaptureWanted && !App.Prefs.DebugFps) StopRendering();
    }
    private void RenderState(double dt)
    {
        double progress = shape.Value, clamped = Math.Clamp(progress, 0, 1), reveal = Ease.Smoothstep(.12, 1, progress);
        double panelHeight = PanelHeight, panelWidth = PanelWidth;
        double toolsReveal = Math.Clamp(workspaceSpring.Value, 0, 1);
        WorkspaceSurface.Opacity = toolsReveal; WorkspaceSurface.IsHitTestVisible = workspaceOpen && toolsReveal > .65;
        WorkspaceScale.ScaleX = WorkspaceScale.ScaleY = Lerp(.92, 1, workspaceSpring.Value); WorkspaceMove.Y = 8 * (1 - toolsReveal);
        ShellScale.ScaleX = panelWidth / 640; ShellScale.ScaleY = panelHeight / 352;
        double surfaceFade = Ease.Smoothstep(.06, .28, progress);
        Shell.Opacity = surfaceFade * (1 - toolsReveal); PillSurface.Opacity = 1 - surfaceFade;
        PillScale.ScaleX = panelWidth / 360; PillScale.ScaleY = panelHeight / 52;
        ArtScale.ScaleX = ArtScale.ScaleY = Lerp(28d / 88, 1, progress);
        ArtMove.X = Lerp(130, 0, progress); ArtMove.Y = Lerp(-80, 0, progress);
        TitleScale.ScaleX = TitleScale.ScaleY = Lerp(13d / 23, 1, progress);
        TitleMove.X = Lerp(59, 0, progress); TitleMove.Y = Lerp(-82, 0, progress);
        double mediaVisibility = Lerp(1, Math.Clamp(pageSprings[0].Value, 0, 1), reveal) * (1 - toolsReveal);
        SharedArt.Opacity = mediaVisibility; TrackTitle.Opacity = mediaVisibility * (1 - Math.Clamp(inlineSpring.Value, 0, 1));
        CompactStatus.Opacity = 1 - clamped; Compact.IsHitTestVisible = !expanded;
        Expanded.Opacity = reveal * (1 - toolsReveal); Expanded.IsHitTestVisible = expanded && !workspaceOpen && toolsReveal < .1 && clamped > .65;
        PeekControls.Opacity = expanded ? 0 : Math.Clamp((clamped - .04) / .10, 0, 1); PeekControls.IsHitTestVisible = !expanded && clamped > .12;
        for (int i = 0; i < pages.Length; i++) SetEntrance(pages[i], pageSprings[i].Value, 0, 12 * (1 - Math.Clamp(pageSprings[i].Value, 0, 1)));
        if (pages[page].RenderTransform is TransformGroup pageTransform) ((TranslateTransform)pageTransform.Children[1]).X = swipeSpring.Value;
        for (int i = 0; i < entries.Length; i++) SetEntrance(entranceElements[i], entries[i].Value, 0, 14 * (1 - Math.Clamp(entries[i].Value, 0, 1)));
        Hud.Opacity = Math.Clamp(hudSpring.Value, 0, 1); HudScale.ScaleX = HudScale.ScaleY = Lerp(.92, 1, hudSpring.Value); HudMove.Y = 12 + panelHeight + 12 - (1 - hudSpring.Value) * 8;
        HudLevel.ScaleX = InlineLevel.ScaleX = Math.Clamp(levelSpring.Value, 0, 1);
        InlineHud.Opacity = Math.Clamp(inlineSpring.Value, 0, 1);
        Visualizer.Opacity = App.Prefs.Visualizer ? 1 : 0;
        for (int i = 0; i < barLevels.Length; i++)
        {
            double target = CaptureWanted ? loopback.Level(i) : 0;
            double blend = 1 - Math.Exp(-dt / (target > barLevels[i] ? .035 : .19));
            barLevels[i] += (target - barLevels[i]) * blend;
            ((ScaleTransform)((Border)Visualizer.Children[i]).RenderTransform).ScaleY = .06 + .94 * barLevels[i];
        }
    }
    private static void SetEntrance(FrameworkElement element, double progress, double x, double y)
    {
        element.Opacity = Math.Clamp(progress, 0, 1); var transform = (TransformGroup)element.RenderTransform;
        var scale = (ScaleTransform)transform.Children[0]; scale.ScaleX = scale.ScaleY = Lerp(.92, 1, progress);
        var translate = (TranslateTransform)transform.Children[1]; translate.X = x; translate.Y = y;
    }
    private bool CaptureWanted => expanded && !workspaceOpen && page == 0 && App.Prefs.Visualizer && !App.Prefs.ReducedMotion && IsVisible && !locked && !disposed;
    private void UpdateCapture() { loopback.SetEnabled(CaptureWanted); if (CaptureWanted) Wake(); }
    private void UpdatePalette()
    {
        Color color = (Color)ColorConverter.ConvertFromString(App.Prefs.Accent);
        if (App.Prefs.AccentFromArtwork && Artwork.Source is System.Windows.Media.Imaging.BitmapSource bitmap) color = ArtworkPalette.Extract(bitmap);
        var brush = new SolidColorBrush(color); brush.Freeze(); Resources["MediaAccent"] = brush;
    }
    private void AdjustVolume(int delta, bool inline)
    {
        try
        {
            audioRevision++; var audio = Audio.Read(); if (!audio.HasValue) return; int level = Math.Clamp(audio.Value.Level + delta, 0, 100); Audio.Set(level);
            bool muted = audio.Value.Muted && level == 0; lastAudio = (level, muted);
            if (App.Prefs.VolumeHud) ShowLevelHud(muted ? "Muted" : "Volume", level, inline || App.Prefs.HudPosition == "Inline when collapsed");
        }
        catch (Exception ex) { ShowHud("!", "Volume unavailable", ex.Message, allowInFocus: true); }
    }
    private void ShowLevelHud(string title, int level, bool inline)
    {
        inlineHud = inline && !expanded; levelSpring.Target = level / 100d;
        if (inlineHud)
        {
            InlineValue.Text = App.Prefs.ShowPercentage ? $"{level}%" : "";
            InlineIcon.Text = title == "Muted" ? "\uE74F" : "\uE767";
            inlineSpring.Target = 1; hudSpring.Target = 0;
        }
        else { ShowHud(title == "Muted" ? "⊘" : "◖", title, "System output", level, true); return; }
        RestartHudTimer(); Wake();
    }
    internal void ShowHud(string icon, string title, string detail = "", int? value = null, bool allowInFocus = false)
    {
        if (locked || !IsVisible || (!allowInFocus && (App.Prefs.Focus || Native.PresentationActive()))) return;
        HudIcon.Text = GlyphFor(icon);
        HudTitle.Text = title; HudDetail.Text = detail; HudValue.Text = value.HasValue && App.Prefs.ShowPercentage ? $"{value}%" : "";
        HudProgress.Visibility = value.HasValue ? Visibility.Visible : Visibility.Collapsed; levelSpring.Target = (value ?? 0) / 100d;
        hudSpring.Target = 1; inlineSpring.Target = 0; RestartHudTimer(); Wake();
    }
    private void RestartHudTimer() { hudClock.Stop(); hudClock.Interval = TimeSpan.FromSeconds(App.Prefs.HudSeconds); hudClock.Start(); }
    internal static string GlyphFor(string icon) => icon switch { "◖" => "\uE767", "⊘" => "\uE74F", "☀" => "\uE706", "ϟ" => "\uE945", "♬" => "\uE702", "♫" => "\uE8D6", "☾" => "\uE708", "✓" => "\uE73E", "!" => "\uE7BA", _ => "\uE7F4" };
    private async Task PollAudio()
    {
        if (disposed || locked || audioBusy) return; audioBusy = true;
        try
        {
            long revision = audioRevision; var audio = await Task.Run(Audio.Read); if (disposed || revision != audioRevision) return;
            if (audio.HasValue && lastAudio.HasValue && audio != lastAudio && App.Prefs.VolumeHud) ShowLevelHud(audio.Value.Muted ? "Muted" : "Volume", audio.Value.Muted ? 0 : audio.Value.Level, App.Prefs.HudPosition == "Inline when collapsed");
            lastAudio = audio;
        }
        finally { audioBusy = false; }
    }
    private void TickTimer()
    {
        if (disposed || locked) return;
        if (timerEnd.HasValue)
        {
            timerSeconds = Math.Max(0, (int)Math.Ceiling((timerEnd.Value - DateTimeOffset.Now).TotalSeconds));
                if (timerSeconds == 0)
                {
                    timerEnd = null; Icons.SetLabel(TimerToggle, "Start"); ShowHud("✓", "A little time, well spent.", "Your focus timer is complete.", allowInFocus: true); System.Media.SystemSounds.Asterisk.Play();
                    if (!breakSession) { WorkspaceData.Current.FocusHistory.Add(new FocusRecord { Date = DateTime.Now, Minutes = focusMinutes }); WorkspaceData.Current.Save(); }
                    if (App.Prefs.ChainPomodoro) { breakSession = !breakSession; timerSeconds = (breakSession ? 5 : focusMinutes) * 60; timerEnd = DateTimeOffset.Now.AddSeconds(timerSeconds); Icons.SetLabel(TimerToggle, "Pause"); }
                }
        }
        TimerText.Text = TimeSpan.FromSeconds(timerSeconds).ToString(@"mm\:ss");
    }
    private static string FormatTime(double seconds) { var time = TimeSpan.FromSeconds(Math.Max(0, seconds)); return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss"); }
    private void BeginScrub() { if (!TrackProgress.IsEnabled) return; scrubbing = true; seekDuration = Services.DurationSeconds; seekKey = Services.MediaKey; }
    private async Task CommitScrub()
    {
        if (!scrubbing) return; scrubbing = false; seekTip.IsOpen = false;
        try { await Services.Seek(TrackProgress.Value, seekDuration, seekKey); } catch (Exception ex) { ShowHud("!", "Seeking unavailable", ex.Message, allowInFocus: true); }
    }
    private async Task Poll()
    {
        if (busy || !ready || locked || disposed) return; busy = true;
        try
        {
            ticks++; CompactStatus.Text = timerEnd.HasValue ? TimerText.Text : DateTime.Now.ToString("HH:mm");
            var media = await Services.ReadMedia(); if (disposed) return;
            TrackTitle.Text = App.Prefs.Focus ? "Focus is on" : media.Title == "Nothing playing" ? "A little space for everything." : media.Title; TrackArtist.Text = media.Artist;
            if (!scrubbing) { TrackProgress.Value = media.Progress; ElapsedTime.Text = FormatTime(Services.PositionSeconds); }
            DurationTime.Text = Services.DurationSeconds > 0 ? FormatTime(Services.DurationSeconds) : "—:——";
            TrackProgress.IsEnabled = Services.CanSeek;
            if (!ReferenceEquals(lastArt, media.Art)) { lastArt = media.Art; Artwork.Source = ArtBackdrop.Source = media.Art; ArtBackdrop.Opacity = media.Art == null ? 0 : .6; UpdatePalette(); }
            UpdatePlaybackVisuals(Services.IsPlaying); Previous.IsEnabled = Services.CanPrevious; Next.IsEnabled = Services.CanNext; Play.IsEnabled = Services.CanPlay || Services.CanPause || Services.CanToggle;
            if (lastTrack.Length > 0 && media.Title != lastTrack && media.Title != "Nothing playing") ShowHud("♫", media.Title, media.Artist); lastTrack = media.Title;
            if (Native.GetSystemPowerStatus(out var power))
            {
                PowerText.Text = power.Percent == 255 || (power.Flag & 128) != 0 ? "Plugged in · no system battery" : $"{power.Percent}% battery · {(power.AC == 1 ? "plugged in" : "on battery")}";
                if (App.Prefs.BatteryAlerts && lastAc.HasValue && power.AC != lastAc) ShowHud("ϟ", power.AC == 1 ? "Power connected" : "On battery", "System battery", power.Percent == 255 ? null : power.Percent);
                if (App.Prefs.BatteryAlerts && power.AC == 0 && power.Percent <= 20 && lastBattery > 20) ShowHud("ϟ", "Battery is running low", "Connect your charger", power.Percent);
                if (power.Percent <= 100 && power.AC == 1 && lastBattery < App.Prefs.FullChargeLevel && power.Percent >= App.Prefs.FullChargeLevel) ShowHud("✓", "Charge target reached", "Ready to unplug", power.Percent);
                lastAc = power.AC; lastBattery = power.Percent;
            }
            if (ticks % 5 == 1 && (ticks == 1 || Services.Brightness.HasValue))
            {
                await Task.Run(Services.ReadBrightness);
                if (Services.Brightness.HasValue && lastBrightness.HasValue && Services.Brightness != lastBrightness && App.Prefs.BrightnessHud) ShowHud("☀", "Brightness", "Built-in display", Services.Brightness, true);
                lastBrightness = Services.Brightness;
            }
            if (ticks % 15 == 1)
            {
                try { var added = await Services.ReadDevices(); if (added != null && App.Prefs.DeviceAlerts) ShowHud("♬", "Device connected", added); } catch (Exception ex) { Services.DeviceSummary = "Bluetooth status unavailable"; Log.Write(ex); }
            }
            var notification = await Services.ReadNotifications(); if (notification.HasValue) { ShowHud("▣", notification.Value.Title, notification.Value.Body); WorkspaceData.Current.NotificationHistory.Insert(0, notification.Value.Title + ": " + notification.Value.Body); if (WorkspaceData.Current.NotificationHistory.Count > 100) WorkspaceData.Current.NotificationHistory.RemoveAt(100); }
            if (ticks == 1 || ticks % 900 == 0) _ = RefreshWeather();
            DeviceText.Text = Services.DeviceSummary;
            if (DateTime.Now.Minute == 0 && DateTime.Now.Second < 2 && page == 1) BuildCalendar();
        }
        catch (Exception ex) { Log.Write(ex); }
        finally { busy = false; }
    }
    private async Task RefreshWeather() { await Services.ReadWeather(); if (!disposed) WeatherText.Text = Services.WeatherSummary; }
    internal void SettlePreview() { SnapMotion(); RenderState(0); StopRendering(); }

    private void BuildCalendar()
    {
        var today = DateTime.Today; DateLabel.Text = today.ToString("dddd").ToUpperInvariant(); DateNumber.Text = today.Day.ToString(); MonthLabel.Text = today.ToString("MMMM yyyy").ToUpperInvariant();
        AgendaSummary.Text = string.Join("\n\n", App.Prefs.Agenda.Where(x => x.When >= DateTime.Now.AddMinutes(-30)).OrderBy(x => x.When).Take(3).Select(x => $"{x.When:ddd HH:mm}  {x.Title}"));
        if (AgendaSummary.Text.Length == 0) AgendaSummary.Text = "A clear day ahead.\nAdd an event in Settings.";
        CalendarGrid.Children.Clear(); foreach (var day in new[] { "M", "T", "W", "T", "F", "S", "S" }) CalendarGrid.Children.Add(new TextBlock { Text = day, FontSize = 10, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 5) });
        var first = new DateTime(today.Year, today.Month, 1); int pad = ((int)first.DayOfWeek + 6) % 7;
        for (int i = 0; i < pad; i++) CalendarGrid.Children.Add(new TextBlock());
        for (int day = 1; day <= DateTime.DaysInMonth(today.Year, today.Month); day++)
        {
            bool selected = day == today.Day;
            CalendarGrid.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = selected ? Brushes.White : Brushes.Transparent, Margin = new Thickness(1), Padding = new Thickness(0, 3, 0, 3), Child = new TextBlock { Text = day.ToString(), FontSize = 11, Foreground = selected ? Brushes.Black : Brushes.White, HorizontalAlignment = HorizontalAlignment.Center } });
        }
    }
    private double PanelWidth => Lerp(360, 640, shape.Value) + 260 * workspaceSpring.Value;
    private double PanelHeight => Lerp(52, 352, shape.Value) + 298 * workspaceSpring.Value;
    internal void OpenWorkspace(string page)
    {
        if (locked || disposed) return;
        Show(); Expand(); collapseClock.Stop(); peekClock.Stop();
        if (!workspaceOpen) { workspace?.Dispose(); workspace = new WorkspaceView(page); WorkspaceHost.Children.Clear(); WorkspaceHost.Children.Add(workspace); }
        else { workspace!.CommandSearch.Clear(); workspace.Select(page == "Commands" ? "Home" : page); }
        if (page == "Commands") workspace!.ShowCommands();
        workspaceOpen = true; workspaceSpring.Target = 1; inlineSpring.Target = 0;
        UpdateCapture(); Wake(); Activate();
        Dispatcher.BeginInvoke(() => { if (workspaceOpen && workspace != null) { if (page == "Commands") workspace.CommandSearch.Focus(); else workspace.Focus(); } });
    }
    internal void CloseWorkspace()
    {
        if (!workspaceOpen) return;
        workspaceOpen = false; workspaceSpring.Target = 0; workspace?.Suspend(); UpdateCapture(); Wake();
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => App.OpenSettings();
    private void ToolsClick(object sender, RoutedEventArgs e) => App.OpenTools("Home");
    private void PlaybackUpdated() { if (!disposed) Dispatcher.BeginInvoke(() => { if (!disposed) UpdatePlaybackVisuals(Services.IsPlaying); }); }
    internal void UpdatePlaybackVisuals(bool playing)
    {
        PlayGlyph.Data = Geometry.Parse(playing ? "M 1,0 L 6,0 6,16 1,16 Z M 11,0 L 16,0 16,16 11,16 Z" : "M 2,0 L 15,8 2,16 Z");
        System.Windows.Automation.AutomationProperties.SetName(Play, playing ? "Pause" : "Play"); Play.ToolTip = playing ? "Pause" : "Play";
        PeekGlyph.Kind = playing ? "pause" : "play"; System.Windows.Automation.AutomationProperties.SetName(PeekPlay, playing ? "Pause" : "Play");
    }
    private void CollapseClick(object sender, RoutedEventArgs e) => Collapse();
    private void MediaTabClick(object sender, RoutedEventArgs e) => ShowPage(0);
    private void TodayTabClick(object sender, RoutedEventArgs e) => ShowPage(1);
    private void ActivityTabClick(object sender, RoutedEventArgs e) => ShowPage(2);
    internal async Task MediaAction(string action) { try { await Services.Control(action); UpdatePlaybackVisuals(Services.IsPlaying); } catch (Exception ex) { ShowHud("!", "Media control unavailable", ex.Message); } }
    private async void PreviousClick(object sender, RoutedEventArgs e) => await MediaAction("previous");
    private async void PlayClick(object sender, RoutedEventArgs e) => await MediaAction("play");
    private async void NextClick(object sender, RoutedEventArgs e) => await MediaAction("next");
    private void TimerClick(object sender, RoutedEventArgs e) { if (timerEnd.HasValue) { timerSeconds = Math.Max(0, (int)Math.Ceiling((timerEnd.Value - DateTimeOffset.Now).TotalSeconds)); timerEnd = null; Icons.SetLabel(TimerToggle, "Resume"); } else { if (timerSeconds == 0) timerSeconds = 1500; timerEnd = DateTimeOffset.Now.AddSeconds(timerSeconds); Icons.SetLabel(TimerToggle, "Pause"); } }
    internal void StartFocus(int minutes) { if (minutes is < 1 or > 180) throw new ArgumentOutOfRangeException(nameof(minutes)); focusMinutes = minutes; breakSession = false; timerSeconds = minutes * 60; timerEnd = DateTimeOffset.Now.AddSeconds(timerSeconds); Icons.SetLabel(TimerToggle, "Pause"); }
    private void ResetClick(object sender, RoutedEventArgs e) { timerEnd = null; timerSeconds = 1500; TimerText.Text = "25:00"; Icons.SetLabel(TimerToggle, "Start"); }
    public void Dispose()
    {
        if (disposed) return;
        workspace?.Dispose(); WorkspaceHost.Children.Clear(); disposed = true; clock.Stop(); audioClock.Stop(); hudClock.Stop(); collapseClock.Stop();
        peekClock.Stop(); StopRendering(); loopback.Dispose(); volumeKeys?.Dispose(); seekTip.IsOpen = false;
        Services.PlaybackChanged -= PlaybackUpdated; Services.Dispose();
        SystemEvents.SessionSwitch -= SessionSwitch; SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        if (handle != IntPtr.Zero) Native.UnregisterHotKey(handle, 1); source?.RemoveHook(Hook);
        if (handle != IntPtr.Zero) { Native.UnregisterHotKey(handle, 2); DesktopNative.RemoveClipboardFormatListener(handle); }
    }
}
