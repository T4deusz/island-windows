using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Island;
// One 24-unit grid, rounded 1.8-unit strokes and generous counters remain legible at 16 px.
public sealed class IconView : Viewbox
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(IconView), new PropertyMetadata("grid", (d, _) => ((IconView)d).Draw()));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IconView() { Width = Height = 16; IsHitTestVisible = false; Draw(); }
    private void Draw()
    {
        var path = new Path { Data = Geometry.Parse(Icons.Paths.TryGetValue(Kind, out var data) ? data : Icons.Paths["grid"]), StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1), FallbackValue = Brushes.White });
        var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(path); Child = canvas;
    }
}
internal static class Icons
{
    internal static readonly Dictionary<string, string> Paths = new()
    {
        ["grid"] = "M4 4H10V10H4Z M14 4H20V10H14Z M4 14H10V20H4Z M14 14H20V20H14Z",
        ["home"] = "M3 11L12 3 21 11 M5 9V21H10V15H14V21H19V9",
        ["play"] = "M8 4L20 12 8 20Z", ["pause"] = "M8 5V19 M16 5V19",
        ["previous"] = "M5 5V19 M19 5L6.88 12 19 19Z", ["next"] = "M19 5V19 M5 5L17.12 12 5 19Z",
        ["folder"] = "M3 8V19Q3 21 5 21H19Q21 21 21 19V8H3Z M3 8V5Q3 3 5 3H10L13 6H19Q21 6 21 8",
        ["clipboard"] = "M8 5H5Q3 5 3 7V20Q3 22 5 22H19Q21 22 21 20V7Q21 5 19 5H16 M8 3H16V7H8Z M7 12H17 M7 16H14",
        ["note"] = "M14 3H5Q3 3 3 5V19Q3 21 5 21H19Q21 21 21 19V10 M10 14L11.07 10 18.07 3 22.07 7 15.07 14Z",
        ["timer"] = "M9 2H15 M12 2V5 M18 6L20 4 M12 6A8 8 0 1 1 11.99 6 M12 10V14L15 16",
        ["search"] = "M11 3A8 8 0 1 1 10.99 3 M17 17L22 22",
        ["snippet"] = "M8 3H19Q21 3 21 5V16 M16 7H5Q3 7 3 9V19Q3 21 5 21H16Q18 21 18 19V9Q18 7 16 7Z M7 12H14 M7 16H12",
        ["launch"] = "M13 3H21V11 M21 3L10 14 M10 3H5Q3 3 3 5V19Q3 21 5 21H19Q21 21 21 19V14",
        ["bell"] = "M5 10A7 7 0 0 1 19 10V16L21 19H3L5 16Z M9 22H15",
        ["camera"] = "M3 7H7L9 4H15L17 7H21V20H3Z M12 10A3.5 3.5 0 1 1 11.99 10",
        ["mixer"] = "M5 3V9 M5 15V21 M12 3V14 M12 20V21 M19 3V5 M19 11V21 M2 9H8V15H2Z M9 14H15V20H9Z M16 5H22V11H16Z",
        ["chart"] = "M4 3V21H22 M8 16V11 M13 16V6 M18 16V3",
        ["code"] = "M7 6L1 12 7 18 M17 6L23 12 17 18 M14.41 3L9.59 21",
        ["settings"] = "M4 5H20 M4 12H20 M4 19H20 M8 3V7 M16 10V14 M10 17V21",
        ["screen"] = "M3 3H21V17H3Z M12 17V21 M7 21H17",
        ["info"] = "M12 2A10 10 0 1 1 11.99 2 M12 11V17 M12 7V7.1",
        ["copy"] = "M8 3H20V16 M4 7H16V21H4Z",
        ["trash"] = "M3 6H21 M9 6V3H15V6 M5 6L6 21H18L19 6 M10 10V17 M14 10V17",
        ["plus"] = "M12 4V20 M4 12H20", ["check"] = "M4 12L9 17 20 6",
        ["close"] = "M5 5L19 19 M19 5L5 19", ["minus"] = "M4 12H20",
        ["back"] = "M14 5L7 12 14 19 M7 12H21", ["up"] = "M5 15L12 8 19 15", ["down"] = "M5 9L12 16 19 9",
        ["refresh"] = "M20 8A9 9 0 1 0 1 12 M20 3V8H15",
        ["save"] = "M4 3H17L21 7V21H3V3Z M7 3V10H17V3 M7 21V15H17V21",
        ["stop"] = "M5 5H19V19H5Z", ["volume"] = "M3 9H7L12 4V20L7 15H3Z M16 8Q20 12 16 16 M19 5Q26 12 19 19",
        ["mic"] = "M9 5A3 3 0 0 1 15 5V12A3 3 0 0 1 9 12Z M5 10V12A7 7 0 0 0 19 12V10 M12 19V22 M8 22H16",
        ["moon"] = "M21 14A9 9 0 0 1 10 3A9 9 0 1 0 21 14Z",
        ["sun"] = "M12 7A5 5 0 1 1 11.99 7 M12 1V3 M12 21V23 M1 12H3 M21 12H23 M4 4L6 6 M18 18L20 20 M4 20L6 18 M18 6L20 4",
        ["battery"] = "M2 6H20V18H2Z M22 10V14 M6 10V14 M10 10V14 M14 10V14",
        ["calendar"] = "M3 5H21V21H3Z M3 10H21 M7 2V7 M17 2V7 M7 14H10 M14 14H17 M7 17H10",
        ["lock"] = "M5 10H19V21H5Z M8 10V6A4 4 0 0 1 16 6V10 M12 14V17",
        ["shuffle"] = "M3 5H6L18 19H22 M18 15L22 19 18 23 M3 19H6L11 13 M14 9L18 5H22 M18 1L22 5 18 9",
        ["repeat"] = "M4 8V5H20V11 M17 8L20 11 23 8 M20 16V19H4V13 M1 16L4 13 7 16",
        ["speed"] = "M3 19A10 10 0 1 1 21 19 M12 14L18 7 M9 19H15",
        ["archive"] = "M3 3H21V8H3Z M5 8V21H19V8 M9 12H15",
        ["eye"] = "M2 12Q12 0 22 12Q12 24 2 12Z M12 9A3 3 0 1 1 11.99 9",
        ["power"] = "M12 2V12 M7 5A9 9 0 1 0 17 5",
        ["bluetooth"] = "M5 6L19 18 12 23V1L19 6 5 18",
        ["text"] = "M4 4H20 M12 4V21 M8 21H16", ["color"] = "M12 2L20.08 16A9.33 9.33 0 0 1 3.92 16Z",
        ["link"] = "M10 8L14 4A4 4 0 0 1 20 10L16 14 M14 16L10 20A4 4 0 0 1 4 14L8 10 M8 16L16 8",
    };
    internal static string KindFor(string label)
    {
        string s = label.ToLowerInvariant();
        if (s.Contains("pause") && !s.Contains("play")) return "pause";
        foreach (var (word, kind) in new (string, string)[] { ("previous", "previous"), ("next", "next"), ("shuffle", "shuffle"), ("repeat", "repeat"), ("speed", "speed"), ("play", "play"), ("today", "calendar"), ("activities", "chart"), ("home", "home"), ("shelf", "folder"), ("clipboard", "clipboard"), ("notes", "note"), ("focus", "moon"), ("search", "search"), ("snippet", "snippet"), ("launcher", "launch"), ("reminder", "bell"), ("camera", "camera"), ("mirror", "camera"), ("mixer", "mixer"), ("usage", "chart"), ("developer", "code"), ("teleprompter", "text"), ("capabilities", "info"), ("workspace", "grid"), ("detach", "launch"), ("back", "back"), ("collapse", "up"), ("reset", "refresh"), ("refresh", "refresh"), ("copy", "copy"), ("remove", "trash"), ("clear", "trash"), ("dismiss", "close"), ("close", "close"), ("deny", "close"), ("minimize", "minus"), ("zoom", "screen"), ("save", "save"), ("add", "plus"), ("allow", "check"), ("complete", "check"), ("stop", "stop"), ("microphone", "mic"), ("volume", "volume"), ("sound", "volume"), ("brightness", "sun"), ("display", "sun"), ("battery", "battery"), ("connectivity", "bluetooth"), ("bluetooth", "bluetooth"), ("lock", "lock"), ("calendar", "calendar"), ("weather", "sun"), ("settings", "settings"), ("system", "screen"), ("general", "settings"), ("about", "info"), ("launch", "launch"), ("open", "launch"), ("up", "up"), ("down", "down"), ("convert", "refresh"), ("extract", "archive"), ("zip", "archive"), ("screenshot", "screen"), ("ocr", "text"), ("read", "text"), ("color", "color"), ("awake", "eye"), ("hide", "eye"), ("quit", "power"), ("start", "play"), ("break", "timer"), ("min", "timer"), ("translate", "text"), ("calculat", "code"), ("token", "lock"), ("left", "back"), ("right", "launch"), ("top", "up"), ("bottom", "down"), ("maximize", "screen") }) if (s.Contains(word)) return kind;
        return "grid";
    }
    internal static StackPanel Label(string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new IconView { Kind = KindFor(label), Margin = new Thickness(0, 0, 8, 0) });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); return row;
    }
    internal static void SetLabel(Button button, string label)
    {
        if (AutomationProperties.GetName(button) == label && button.Content is StackPanel) return;
        button.Content = Label(label); AutomationProperties.SetName(button, label);
    }
    internal static void Apply(DependencyObject root)
    {
        if (root is Button button) Enhance(button);
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray()) Apply(child);
    }
    internal static void Enhance(Button button)
    {
        if (button.Content is string label && label.Length > 0) { SetLabel(button, label.Replace("⌘  ", "").Replace("  ↗", "")); return; }
        if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
        {
            if (button.Content is StackPanel row && row.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock text) AutomationProperties.SetName(button, text.Text);
            else if (button.ToolTip is string tooltip) AutomationProperties.SetName(button, tooltip);
        }
        if (button.Content == null && button.ToolTip is string hint) { button.Content = new IconView { Kind = KindFor(hint), Width = 8, Height = 8 }; AutomationProperties.SetName(button, hint); }
    }
}
