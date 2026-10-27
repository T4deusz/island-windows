using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Island;
public partial class SettingsWindow : Window
{
    private string selected = "General";
    private static Preferences P => App.Prefs;
    private static DesktopServices S => App.Island.Services;
    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DesktopNative.ApplyCapturePrivacy(this);
        foreach (var item in new[] { "General", "— NOTIFICATIONS", "Battery", "Connectivity", "Focus", "Display", "Sound", "— LIVE ACTIVITIES", "Now Playing", "Calendar & Weather", "Lock Screen", "— ISLAND", "About" })
        {
            if (item.StartsWith('—')) { Navigation.Children.Add(new TextBlock { Text = item[2..], FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(14, 22, 0, 8) }); continue; }
            var label = Icons.Label(item);
            var button = new Button { Content = label, Tag = item, ToolTip = item, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 2, 0, 2), Background = Brushes.Transparent };
            button.Click += (_, _) => Select(item); Navigation.Children.Add(button);
        }
        Select("General"); Icons.Apply(this);
    }
    private void Save() { P.Save(); App.Island.ApplyPreferences(); }
    internal void Select(string item)
    {
        selected = item; SectionTitle.Text = item; ContentPanel.Children.Clear();
        foreach (var b in Navigation.Children.OfType<Button>()) { bool active = (string)b.Tag == item; b.Background = active ? new SolidColorBrush(Color.FromRgb(53, 53, 59)) : Brushes.Transparent; b.Foreground = active ? (Brush)Application.Current.Resources["Accent"] : Brushes.White; }
        SectionSubtitle.Text = item switch { "General" => "Your desktop, with a little more possibility.", "Sound" => "Less interruption. Just the right amount of information.", "Display" => "A brighter way to see your display controls.", "Battery" => "Know when to plug in. And when you're free to go.", "Connectivity" => "A warm welcome for your favorite devices.", "Focus" => "Make a little room for concentration.", "Now Playing" => "Your soundtrack, always within reach.", "Calendar & Weather" => "A glance at the day ahead.", "Lock Screen" => "Private when it matters most.", _ => "Native Windows. Inspired by the island experience." };
        switch (item)
        {
            case "General": General(); break;
            case "Sound": Sound(); break;
            case "Display": Display(); break;
            case "Battery": Battery(); break;
            case "Connectivity": Connectivity(); break;
            case "Focus": FocusSettings(); break;
            case "Now Playing": Media(); break;
            case "Calendar & Weather": CalendarWeather(); break;
            case "Lock Screen": LockScreen(); break;
            default: About(); break;
        }
    }
    private StackPanel Card(string? title = null)
    {
        var panel = new StackPanel();
        ContentPanel.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(34, 34, 39)), BorderBrush = new SolidColorBrush(Color.FromRgb(49, 49, 55)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 16), Child = panel });
        if (title != null) panel.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        return panel;
    }
    private static TextBlock Text(Panel panel, string text, bool muted = true)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = muted ? new SolidColorBrush(Color.FromRgb(157, 157, 169)) : Brushes.White, Margin = new Thickness(0, 5, 0, 8), LineHeight = 20 }; panel.Children.Add(t); return t;
    }
    private void Toggle(Panel panel, string label, bool value, Action<bool> set)
    {
        var c = new CheckBox { Content = label, IsChecked = value }; c.Checked += (_, _) => { set(true); Save(); }; c.Unchecked += (_, _) => { set(false); Save(); }; panel.Children.Add(c);
    }
    private static Button Action(Panel panel, string label, Action callback)
    {
        var button = new Button { Content = Icons.Label(label), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) }; button.Click += (_, _) => { try { callback(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Island", MessageBoxButton.OK, MessageBoxImage.Information); } }; panel.Children.Add(button); return button;
    }
    private void Range(Panel panel, string label, double value, double min, double max, Action<double> change, string format = "0")
    {
        var caption = Text(panel, $"{label}   {value.ToString(format)}", false); var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = .1, IsSnapToTickEnabled = true };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { caption.Text = $"{label}   {slider.Value.ToString(format)}"; change(slider.Value); Save(); }; panel.Children.Add(slider);
    }
    private void Preview(Panel panel, string icon, string name, string detail, int? value = null)
    {
        var preview = new DockPanel();
        preview.Children.Add(new TextBlock { Text = IslandWindow.GlyphFor(icon), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), Width = 40, FontSize = 20, Foreground = (Brush)Application.Current.Resources["Accent"] });
        var percentage = new TextBlock { Text = value.HasValue ? $"{value}%" : "", FontSize = 17, Foreground = (Brush)Application.Current.Resources["Accent"] }; DockPanel.SetDock(percentage, Dock.Right); preview.Children.Add(percentage);
        preview.Children.Add(new TextBlock { Text = name, FontSize = 17 });
        var border = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(17), Padding = new Thickness(24), Margin = new Thickness(0, 5, 0, 10), Child = preview }; panel.Children.Add(border);
        Action(panel, "Preview animation", () => App.Island.ShowHud(icon, name, detail, value, true));
    }
    private void General()
    {
        var card = Card("Make yourself at home");
        Text(card, "A black, animated island at the top of your screen. Click or hover to open it. Right-click for settings.");
        Toggle(card, "Peek on hover · click to fully expand", P.HoverExpand, v => P.HoverExpand = v);
        Toggle(card, "Collapse when the pointer leaves", P.AutoHide, v => P.AutoHide = v);
        Toggle(card, "Reduce motion", P.ReducedMotion, v => P.ReducedMotion = v);
        Toggle(card, "macOS-style attached notch", P.NotchMode, v => P.NotchMode = v);
        Toggle(card, "Horizontal swipe navigation", P.HorizontalGestures, v => P.HorizontalGestures = v);
        Toggle(card, "Vertical expand / collapse gestures", P.VerticalGestures, v => P.VerticalGestures = v);
        Action(card, "Open the macOS-style workspace", () => App.OpenTools("Home"));
        var motion = Card("Motion & rendering");
        Choice(motion, "Animation style", new[] { "Spring", "Gentle", "Instant" }, P.AnimationStyle, v => P.AnimationStyle = v);
        Text(motion, "Response-based springs use different presets for opening, closing, peeking, HUDs and tracking. Gentle removes bounce. Reduced motion overrides both. Surfaces, shared visuals and content animate through transforms and opacity.");
        Toggle(motion, "Accent from album artwork", P.AccentFromArtwork, v => P.AccentFromArtwork = v);
        Toggle(motion, "Live system audio visualizer", P.Visualizer, v => P.Visualizer = v);
        Toggle(motion, "Debug FPS overlay", P.DebugFps, v => P.DebugFps = v);
        Text(motion, $"WPF graphics tier {RenderCapability.Tier >> 16}. Hardware acceleration uses the system's available GPU/driver; tier 0 falls back to software. The debug counter measures render callbacks, not presented display frames. Debug mode intentionally keeps rendering active.");
        var hud = Card("HUD placement");
        Choice(hud, "Level HUD position", new[] { "Below island", "Inline when collapsed" }, P.HudPosition, v => P.HudPosition = v);
        Toggle(hud, "Replace standard volume-key flyouts", P.ReplaceVolumeKeys, v => P.ReplaceVolumeKeys = v);
        Text(hud, "Intercepts standard physical volume/mute keys while Island is visible and unlocked. Vendor hotkeys, brightness keys and elevated apps may still show Windows' flyout. No shell patching. Scrolling the pill always uses its inline HUD.");
        var widgets = Card("Widget order");
        Text(widgets, "Tab order, page dots and swipe navigation follow this order.");
        for (int i = 0; i < P.WidgetOrder.Count; i++)
        {
            int position = i; var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var up = new Button { Content = new IconView { Kind = "up" }, ToolTip = "Move widget up", Padding = new Thickness(12, 7, 12, 7), IsEnabled = i > 0 };
            var down = new Button { Content = new IconView { Kind = "down" }, ToolTip = "Move widget down", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 0, 0), IsEnabled = i < 2 };
            DockPanel.SetDock(down, Dock.Right); DockPanel.SetDock(up, Dock.Right); row.Children.Add(down); row.Children.Add(up);
            Text(row, new[] { "Now playing", "Today", "Activities" }[P.WidgetOrder[i]], false); widgets.Children.Add(row);
            up.Click += (_, _) => MoveWidget(position, -1); down.Click += (_, _) => MoveWidget(position, 1);
        }
        var layout = Card("Placement & personality");
        Range(layout, "Island size", P.Scale, .8, 1.5, v => P.Scale = v, "0.0×");
        Range(layout, "Distance from top", P.Offset, 0, 150, v => P.Offset = v, "0 px");
        Text(layout, "Display"); var monitors = new ComboBox { ItemsSource = Forms.Screen.AllScreens.Select((s, i) => $"Display {i + 1} · {s.Bounds.Width} × {s.Bounds.Height}").ToArray(), SelectedIndex = Math.Clamp(P.Monitor, 0, Forms.Screen.AllScreens.Length - 1), Margin = new Thickness(0, 2, 0, 16) };
        monitors.SelectionChanged += (_, _) => { P.Monitor = monitors.SelectedIndex; Save(); }; layout.Children.Add(monitors);
        var colors = new StackPanel { Orientation = Orientation.Horizontal }; layout.Children.Add(colors);
        foreach (var color in new[] { "#83E2C6", "#BBA6FF", "#F3A7C2", "#8CCBFF", "#FFD58A", "#FFFFFF" })
        {
            var b = new Button { Content = new IconView { Kind = "color", Width = 14, Height = 14 }, Foreground = Brushes.Black, Padding = new Thickness(0), Width = 34, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), ToolTip = color }; b.Click += (_, _) => { P.Accent = color; Save(); }; colors.Children.Add(b);
            System.Windows.Automation.AutomationProperties.SetName(b, "Accent " + color);
        }
        var shortcuts = Card("A few little gestures");
        Text(shortcuts, $"{App.Island.HotkeyStatus} · expand / collapse\nSwipe left / right · change page\nSwipe up / down · collapse / expand\nScroll over the island · adjust volume\nSystem tray · settings, hide or quit\nArrow keys and Escape work when the island has keyboard focus.");
        Action(shortcuts, "Create a startup shortcut", () =>
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("App path unavailable.");
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            try { dynamic link = shell.CreateShortcut(Path.Combine(startup, "Island.lnk")); try { link.TargetPath = exe; link.WorkingDirectory = Path.GetDirectoryName(exe); link.Save(); } finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(link); } } finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); }
            MessageBox.Show("Island will open when you sign in. Keep this app folder in its current location.", "Island");
        });
        Action(shortcuts, "Remove startup shortcut", () => File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Island.lnk")));
    }
    private void Choice(Panel panel, string label, string[] options, string current, Action<string> set)
    {
        Text(panel, label, false); var choice = new ComboBox { ItemsSource = options, SelectedItem = current, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(8) };
        System.Windows.Automation.AutomationProperties.SetName(choice, label);
        choice.SelectionChanged += (_, _) => { if (choice.SelectedItem is string value) { set(value); Save(); } }; panel.Children.Add(choice);
    }
    private void MoveWidget(int position, int direction)
    {
        int target = Math.Clamp(position + direction, 0, 2);
        (P.WidgetOrder[position], P.WidgetOrder[target]) = (P.WidgetOrder[target], P.WidgetOrder[position]); Save(); Select("General");
    }
    private void HudOptions(Panel panel)
    {
        Range(panel, "HUD duration", P.HudSeconds, 1, 10, v => P.HudSeconds = v, "0.0 s");
        Toggle(panel, "Show percentage", P.ShowPercentage, v => P.ShowPercentage = v);
        Text(panel, "Standard volume-key flyouts can be replaced in General. Brightness and vendor-specific flyouts may still appear. Island does not patch Windows shell components.");
    }
    private void Sound()
    {
        var card = Card("Sound"); Toggle(card, "Show volume changes in the island", P.VolumeHud, v => P.VolumeHud = v); Preview(card, "◖", "Volume", "System output", 65);
        var control = Card("System output"); var current = Audio.Read();
        if (current.HasValue)
        {
            Text(control, "Adjust your actual system volume"); var slider = new Slider { Value = current.Value.Level };
            System.Windows.Automation.AutomationProperties.SetName(slider, "System volume"); slider.ValueChanged += (_, _) => { try { Audio.Set((int)slider.Value); } catch (Exception ex) { Log.Write(ex); } }; control.Children.Add(slider);
        }
        else Text(control, "No active audio output device.");
        HudOptions(Card("HUD appearance"));
    }
    private void Display()
    {
        var card = Card("Display"); Toggle(card, "Show brightness changes in the island", P.BrightnessHud, v => P.BrightnessHud = v); Preview(card, "☀", "Brightness", "Built-in display", 80);
        var control = Card("Brightness control"); Text(control, S.BrightnessStatus);
        if (S.Brightness.HasValue)
        {
            var slider = new Slider { Value = S.Brightness.Value }; control.Children.Add(slider); System.Windows.Automation.AutomationProperties.SetName(slider, "Display brightness");
            // shortcut: commit brightness on release to avoid flooding the monitor's WMI provider.
            slider.PreviewMouseLeftButtonUp += async (_, _) => await SetBrightness((int)slider.Value);
            slider.PreviewKeyUp += async (_, _) => await SetBrightness((int)slider.Value);
        }
        Text(control, "Built-in displays use Windows WMI brightness control. External monitors without that interface are not controlled by this version."); HudOptions(Card("HUD appearance"));
    }
    private static async Task SetBrightness(int value) { try { await Task.Run(() => S.SetBrightness(value)); } catch (Exception ex) { MessageBox.Show(ex.Message, "Brightness"); } }
    private void Battery()
    {
        var card = Card("Power, without the interruption"); Toggle(card, "Show charger and low-battery alerts", P.BatteryAlerts, v => P.BatteryAlerts = v); Preview(card, "ϟ", "Power connected", "System battery", 82);
        Text(Card("Live status"), App.Island.PowerText.Text + "\nLow battery alerts appear when the battery crosses 20%.");
        Range(Card("Full-charge target"), "Notify at", P.FullChargeLevel, 80, 100, v => P.FullChargeLevel = (int)v, "0'%'");
    }
    private void Connectivity()
    {
        var card = Card("Your devices"); Toggle(card, "Show Bluetooth connection alerts", P.DeviceAlerts, v => P.DeviceAlerts = v); Preview(card, "♬", "Device connected", "Preview accessory");
        Text(Card("Connected now"), S.DeviceSummary); Text(Card("Device support"), "Connected Bluetooth devices are checked every 15 seconds. Battery appears only when the device and Windows driver expose it. AirPods-specific pairing graphics and proprietary battery readings are not available.");
        Action(card, "Open Windows Bluetooth settings", () => Open("ms-settings:bluetooth"));
    }
    private void FocusSettings()
    {
        var card = Card("Quiet, on your terms"); Toggle(card, "Island quiet mode", P.Focus, v => { P.Focus = v; App.Island.ShowHud("☾", v ? "Focus is on" : "Focus is off", "Island quiet mode", allowInFocus: true); });
        Text(card, "Quiet mode suppresses island notifications, track changes, and device/battery alerts. Volume, brightness and timer completion remain visible. This controls Island only.");
        Text(card, "Windows does not expose a supported general desktop API for reading every Focus / Do not disturb mode. Island also suppresses popups when Windows reports a presentation or full-screen activity.");
        Action(card, "Open Windows Focus settings", () => Open("ms-settings:quiethours"));
        var notifications = Card("Instant notifications"); Text(notifications, S.NotificationStatus);
        Text(notifications, "Reading notifications requires the MSIX edition and your Windows permission. Notification content is displayed locally and never saved. Old notifications are not replayed.");
        Action(notifications, "Connect Windows notifications…", async () => { await S.EnableNotifications(); Select(selected); });
        Toggle(notifications, "Mirror new notifications", P.Notifications, v => { P.Notifications = v; });
        Action(notifications, "Preview a notification", () => App.Island.ShowHud("▣", "You're all set.", "This is a preview notification.", allowInFocus: true));
    }
    private void Media()
    {
        var card = Card("One home for your soundtrack"); Text(card, "Real track information, artwork, playback progress and previous / play-pause / next controls from Windows media sessions. Works with apps and browsers that publish Windows media controls.");
        Action(card, "Open Now Playing", () => { App.Island.MediaTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); App.Island.Expand(); });
        Text(Card("Live audio spectrum"), "Eight frequency bands measure the actual default system output using WASAPI loopback and an FFT, with fast attack and smooth falloff. Capture runs only while Now Playing is fully expanded and the visualizer is enabled. No microphone access, recording files, or audio uploads. Protected or exclusive-mode audio may not be captured.\n" + App.Island.VisualizerStatus);
        Text(Card("Live activities"), "Activities includes a pauseable 25-minute focus timer, battery, Bluetooth devices, and optional live weather. The timer uses an absolute deadline, so it stays accurate across sleep. Timers are not restored after the app exits.");
    }
    private void CalendarWeather()
    {
        var weather = Card("Weather"); Toggle(weather, "Enable live weather from Open-Meteo", P.Weather, v => P.Weather = v);
        Text(weather, "City (only this query is sent to Open-Meteo; no automatic location tracking)"); var city = new TextBox { Text = P.City }; weather.Children.Add(city);
        var weatherStatus = Text(weather, S.WeatherSummary);
        Action(weather, "Update weather", async () => { if (string.IsNullOrWhiteSpace(city.Text)) { weatherStatus.Text = "Enter a city first."; return; } P.City = city.Text.Trim(); Save(); await S.ReadWeather(); weatherStatus.Text = S.WeatherSummary; App.Island.WeatherText.Text = S.WeatherSummary; });
        Text(weather, "Weather data: Open-Meteo.com · CC BY 4.0. Free API for non-commercial use. Refreshes every 15 minutes while running.");
        var agenda = Card("Your agenda · stored on this PC");
        Text(agenda, "Add a local event. Date format: yyyy-MM-dd HH:mm. This version does not connect to Outlook or Google Calendar.");
        var title = new TextBox { Text = "", ToolTip = "Event title" }; agenda.Children.Add(title); System.Windows.Automation.AutomationProperties.SetName(title, "Event title");
        var when = new TextBox { Text = DateTime.Today.AddDays(1).AddHours(9).ToString("yyyy-MM-dd HH:mm") }; agenda.Children.Add(when); System.Windows.Automation.AutomationProperties.SetName(when, "Event date and time");
        Action(agenda, "Add event", () => { if (string.IsNullOrWhiteSpace(title.Text) || !DateTime.TryParseExact(when.Text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { MessageBox.Show("Enter an event title and a date like 2026-10-27 09:00.", "Event"); return; } if (P.Agenda.Count >= 500) throw new InvalidOperationException("Your agenda is full. Remove old events first."); P.Agenda.Add(new AgendaItem { Title = title.Text.Trim(), When = date }); Save(); Select(selected); });
        foreach (var entry in P.Agenda.OrderBy(x => x.When).ToArray())
        {
            var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; var delete = new Button { Content = Icons.Label("Remove"), Padding = new Thickness(10, 5, 10, 5) }; DockPanel.SetDock(delete, Dock.Right); row.Children.Add(delete); Text(row, $"{entry.When:MMM d HH:mm}  {entry.Title}", false); agenda.Children.Add(row); delete.Click += (_, _) => { P.Agenda.Remove(entry); Save(); Select(selected); };
        }
    }
    private void LockScreen()
    {
        var card = Card("Your lock screen stays secure");
        Text(card, "Windows does not allow a normal desktop app to place an interactive overlay on its secure lock screen. Island automatically hides when you lock your session, and returns when you unlock.");
        Text(card, "For supported lock screen weather and status widgets, use Windows personalization. Island cannot supply custom lock screen widgets in this edition.");
        Action(card, "Open lock screen settings", () => Open("ms-settings:lockscreen"));
        Preview(Card("Unlock-style animation · preview only"), "▣", "Welcome back.", "Your space is ready.");
    }
    private void About()
    {
        var card = Card("Island 1.2"); Text(card, "A native C# / WPF desktop app for Windows 10 (19041+) and Windows 11. Inspired by the interaction and visual style of Alcove. An independent app, with no affiliation to Alcove or Apple.");
        Text(card, "Runs locally without a browser runtime. Settings and local events are stored in your Windows profile. No telemetry. Weather is the only network feature, and is off until you enable it.");
        Text(Card("Platform boundaries"), "• Secure lock screen overlays: unavailable\n• Exact macOS Focus detection: unavailable\n• Notifications: MSIX + permission required\n• External monitor brightness: WMI support required\n• Bluetooth battery: device / driver dependent\n• Volume replacement: standard physical keys only\n• Brightness/vendor flyouts: may still appear\n• Calendar: local events, no account sync\n• Frame rate and CPU depend on hardware/driver");
        Action(card, "Open local settings folder", () => { Directory.CreateDirectory(Preferences.Folder); Open(Preferences.Folder); });
        Action(card, "Quit Island", () => Application.Current.Shutdown());
    }
    private static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    private void OpenClick(object sender, RoutedEventArgs e) { App.Island.Show(); App.Island.Expand(); }
    private void MacTitleDrag(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is Grid) DragMove(); }
    private void MacClose(object sender, RoutedEventArgs e) => Close();
    private void MacMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MacZoom(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
