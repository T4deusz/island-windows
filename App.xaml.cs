using System;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Island;
public partial class App : Application
{
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    internal static Preferences Prefs = Preferences.Load();
    internal static IslandWindow Island = null!;
    internal static SettingsWindow? Settings;
    internal static WorkspaceServices Workspace = null!;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => Icons.Enhance((System.Windows.Controls.Button)sender)));
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        if (Array.IndexOf(e.Args, "--self-test") >= 0)
        {
            try { SelfTest.Run(); Shutdown(); }
            catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "self-test.txt"), "FAIL\n" + ex); Shutdown(1); }
            return;
        }
        if (Array.IndexOf(e.Args, "--diagnostics") >= 0) { await SelfTest.Diagnostics(); Shutdown(); return; }
        if (Array.IndexOf(e.Args, "--api-self-test") >= 0) { try { await SelfTest.Api(); Shutdown(); } catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "api-self-test.txt"), "FAIL\n" + ex); Shutdown(1); } return; }
        if (Array.IndexOf(e.Args, "--performance-test") >= 0) { await SelfTest.Performance(); Shutdown(); return; }
        mutex = new Mutex(true, "Local\\Island.Windows.Desktop", out bool first);
        if (!first) { MessageBox.Show("Island is already running. Open its settings from the system tray.", "Island"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) => { Log.Write(args.Exception); args.Handled = true; };
        Island = new IslandWindow(); MainWindow = Island; Island.Show();
        Workspace = new WorkspaceServices(); Workspace.Configure();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Island", null, (_, _) => { Island.Show(); Island.Expand(); });
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Workspace / command palette", null, (_, _) => OpenTools("Home"));
        menu.Items.Add("Hide / show", null, (_, _) => { if (Island.IsVisible) Island.Hide(); else Island.Show(); });
        menu.Items.Add("Quit Island", null, (_, _) => Shutdown());
        tray = new Forms.NotifyIcon { Text = "Island · your desktop, at a glance", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => OpenSettings();
        if (!Prefs.Onboarded || Array.IndexOf(e.Args, "--settings") >= 0) OpenSettings();
        Prefs.Onboarded = true; Prefs.Save();
    }
    internal static void OpenSettings() { if (Settings == null) { Settings = new SettingsWindow(); Settings.Closed += (_, _) => Settings = null; } Settings.Show(); Settings.Activate(); }
    internal static void OpenTools(string page) { if (Workspace == null) Workspace = new WorkspaceServices(); Island.OpenWorkspace(page); }
    protected override void OnExit(ExitEventArgs e) { tray?.Dispose(); Workspace?.Dispose(); Island?.Dispose(); mutex?.Dispose(); base.OnExit(e); }
}
