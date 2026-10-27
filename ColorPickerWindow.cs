using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Island;
internal sealed class ColorPickerWindow : Window
{
    internal ColorPickerWindow(Action<string> selected)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(Application.Current.MainWindow); Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        Content = new Border { Background = new SolidColorBrush(Color.FromArgb(230, 20, 20, 25)), Padding = new Thickness(20), CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 80, 0, 0), Child = new TextBlock { Text = "Click a screen pixel to copy its color · Escape to cancel", FontSize = 16 } };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_, _) =>
        {
            var point = Native.CursorPosition(); Hide(); var dc = DesktopNative.GetDC(IntPtr.Zero);
            try { uint value = DesktopNative.GetPixel(dc, (int)point.X, (int)point.Y); if (value == 0xFFFFFFFF) throw new InvalidOperationException("This screen pixel is unavailable."); selected($"#{value & 255:X2}{(value >> 8) & 255:X2}{(value >> 16) & 255:X2}"); }
            finally { DesktopNative.ReleaseDC(IntPtr.Zero, dc); Close(); }
        };
    }
}
