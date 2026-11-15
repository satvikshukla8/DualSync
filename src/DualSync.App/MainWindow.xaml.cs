using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DualSync.Audio;
using DualSync.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DualSync.App;

public partial class MainWindow : Window
{
    private readonly AudioEngine engine = new();
    private readonly ShellViewModel model;
    private readonly SettingsStore store;
    private AppSettings settings;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer monitor = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer syncMonitor = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch syncClock = Stopwatch.StartNew();
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private bool loading = true, wanted, quitting, closed, ticking, shutdownStarted;
    private int ticks;
    private DateTime nextRetry;
    private readonly Dictionary<string, string> deviceNames = [];
    private string? lastError;
    public static string DataDirectory { get; } = ResolveDataDirectory();
    private static string ResolveDataDirectory()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--data-dir");
        return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1])
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DualSync");
    }
    public MainWindow()
    {
        store = new(Path.Combine(DataDirectory, "settings.json")); settings = store.Load();
        model = new(settings.Current, settings.EqPresets, settings.DualEqPresets); DataContext = model;
        InitializeComponent();
        MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 24);
        MinHeight = Math.Min(MinHeight, MaxHeight); Height = Math.Min(Height, MaxHeight);
        TrayPreference.IsChecked = settings.MinimizeToTray;
        StartMinimizedPreference.IsChecked = settings.StartMinimized;
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            StartupPreference.IsChecked = key?.GetValue("DualSync") is string;
        ProfileName.Text = settings.Current.Name;
        UpdateProfileNames();
        model.Changed += SettingsChanged;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellViewModel.AutoSyncEnabled) or nameof(ShellViewModel.Running) or nameof(ShellViewModel.Busy) or "")
                UpdateAutoSyncTimer();
        };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        engine.SourceVolumeChanged += value => Dispatcher.BeginInvoke(() =>
        { if (model.MasterModeIndex == (int)MasterMode.SourceEndpoint) model.MirrorSourceVolume(value); });
        engine.Faulted += message => Dispatcher.BeginInvoke(async () =>
        {
            lastError = message; Log(message); model.Status = message + " · Waiting to reconnect.";
            model.Busy = true;
            try { await engine.StopAsync(); } catch (Exception ex) { Log(ex.ToString()); }
            finally { model.Running = false; model.Busy = false; nextRetry = DateTime.UtcNow.AddSeconds(5); }
        });
        monitor.Tick += async (_, _) => await MonitorAsync();
        syncMonitor.Tick += (_, _) => SampleAutoSync();
        Loaded += async (_, _) =>
        {
            await RefreshDevicesAsync(); CreateTray(); loading = false; monitor.Start(); UpdateAutoSyncTimer();
            if (store.LoadWarning is not null) model.Status = store.LoadWarning;
            if (model.SourceId is null) WorkspaceTabs.SelectedItem = SetupTab;
            if (settings.StartMinimized) Hide();
        };
        Closing += Window_Closing;
    }
    private void SettingsChanged(string property)
    {
        if (loading) return;
        engine.Update(model.Snapshot);
        if (property == nameof(ShellViewModel.Master) && model.MasterModeIndex == (int)MasterMode.SourceEndpoint)
        {
            if (engine.IsRunning)
            {
                try { engine.SetSourceVolume(model.Master / 100); }
                catch (Exception ex) { ShowError("Could not change Windows source volume", ex); }
            }
            else model.Status = "Start audio to control the source's Windows volume. Its current level will be read at startup.";
        }
        saveTimer.Stop(); saveTimer.Start();
    }
    private void Save()
    {
        if (loading) return;
        settings = settings with
        {
            Current = model.Snapshot, EqPresets = model.EqLibrary.Singles.ToList(), DualEqPresets = model.EqLibrary.Duals.ToList(),
            MinimizeToTray = TrayPreference.IsChecked == true, StartMinimized = StartMinimizedPreference.IsChecked == true
        };
        try { store.Save(settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("Could not save settings", ex); }
    }
    private async Task StartAsync()
    {
        if (model.Busy) return;
        model.Busy = true;
        try
        {
            model.Status = "Starting audio…";
            await engine.StartAsync(model.Snapshot);
            model.Running = true;
            if (model.MasterModeIndex == (int)MasterMode.SourceEndpoint) model.MirrorSourceVolume(engine.SourceVolume);
            model.Status = PlaybackStatus();
            lastError = null; wanted = true;
        }
        catch (Exception ex) { ShowError("Audio could not start", ex); model.Running = false; nextRetry = DateTime.UtcNow.AddSeconds(10); }
        finally { model.Busy = false; }
    }
    private async Task StopAsync(bool keepIntent = false)
    {
        if (!keepIntent) wanted = false;
        model.Busy = true;
        try { await engine.StopAsync(); model.Status = keepIntent ? "Device disconnected · Waiting to reconnect." : "Stopped · settings are saved"; }
        catch (Exception ex) { ShowError("Could not stop audio cleanly", ex); }
        finally
        {
            model.Running = false; model.Busy = false; model.A.Meter = model.B.Meter = 0;
            model.A.UpdateQueue(null); model.B.UpdateQueue(null);
            model.A.Telemetry = model.B.Telemetry = "Stopped";
        }
    }
    private async void Playback_Click(object sender, RoutedEventArgs e)
    {
        if (engine.IsRunning || wanted) await StopAsync(); else await StartAsync();
    }
    private async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await Task.Run(DeviceCatalog.Enumerate);
            var snapshot = model.Snapshot;
            foreach (var device in devices) deviceNames[device.Id] = device.Name;
            var selected = new[] { snapshot.SourceId, snapshot.Speakers[0].DeviceId, snapshot.Speakers[1].DeviceId };
            foreach (var id in selected.Where(id => id is not null).Distinct())
                if (!devices.Any(d => d.Id == id)) devices.Add(new(id!, deviceNames.GetValueOrDefault(id!, "Saved audio device"), false, false));
            if (model.Devices.Select(d => d.ToString() + d.Id).SequenceEqual(devices.Select(d => d.ToString() + d.Id))) return;
            var wasLoading = loading; loading = true;
            try
            {
                model.Devices.Clear(); foreach (var device in devices) model.Devices.Add(device);
                model.Load(snapshot);
            }
            finally { loading = wasLoading; UpdateAutoSyncTimer(); }
            if (engine.IsRunning && selected.Any(id => id is null || !devices.Any(d => d.Available && d.Id == id)))
                await StopAsync(true);
        }
        catch (Exception ex) { lastError = ex.Message; model.Status = "Could not refresh audio devices: " + ex.Message; Log(ex.ToString()); }
    }
    private void UpdateAutoSyncTimer() => syncMonitor.IsEnabled = !loading && !shutdownStarted && !closed
        && model.AutoSyncEnabled && model.Running && !model.Busy;
    private void SampleAutoSync()
    {
        if (!engine.IsRunning) { model.ResetAutoSync(); return; }
        var stats = engine.Statistics();
        if (stats.Length != 2 || stats[0].Speaker != 0 || stats[1].Speaker != 1) { model.ResetAutoSync(); return; }
        model.ObserveAutoSync(new(syncClock.Elapsed, engine.CapturedFrames, stats[0].BufferedFrames, stats[1].BufferedFrames,
            stats[0].UnderrunFrames, stats[1].UnderrunFrames, stats[0].OverrunFrames, stats[1].OverrunFrames));
    }
    private async Task MonitorAsync()
    {
        if (ticking || model.Busy || closed) return;
        ticking = true;
        try
        {
            if (engine.IsRunning) engine.RefreshSourceVolume();
            if (engine.IsRunning && model.Status.StartsWith("Playing", StringComparison.Ordinal)) model.Status = PlaybackStatus();
            if (++ticks % 4 == 0) await RefreshDevicesAsync();
            var stats = engine.Statistics();
            if (stats.Length == 0)
            {
                model.A.UpdateQueue(null); model.B.UpdateQueue(null);
                model.A.Meter = model.B.Meter = 0;
            }
            foreach (var stat in stats)
            {
                var speaker = stat.Speaker == 0 ? model.A : model.B;
                speaker.Meter = stat.Peak * 100;
                speaker.UpdateQueue(stat.BufferedFrames);
                speaker.Telemetry = $"Peak protection {stat.LimiterReductionDb:0.#} dB · safety clamp {stat.LimitedSamples:N0} samples";
            }
            model.Diagnostic = $"Engine: {(engine.IsRunning ? "playing" : "stopped")}   Capture: {engine.CapturedFrames:N0} frames\nSource Windows volume: {engine.SourceVolume:P0} · muted: {engine.SourceMuted}\n48,000 Hz · float stereo · shared-mode outputs · 80 ms output buffer + 5 ms peak protection\n" +
                string.Join("\n", stats.Select(s => $"Speaker {(s.Speaker == 0 ? "A" : "B")}: queue {s.BufferedFrames / 48.0:0.#} ms | missing {s.UnderrunFrames:N0} | dropped {s.OverrunFrames:N0} | clamped {s.LimitedSamples:N0}")) +
                "\nSync: " + model.AutoSyncStatus + "\n" + model.AutoSyncDetails +
                (lastError is null ? "" : "\nLast error: " + lastError);
            if (wanted && !engine.IsRunning && !model.Busy && DateTime.UtcNow >= nextRetry)
            {
                nextRetry = DateTime.UtcNow.AddSeconds(10);
                try { RouteValidation.Validate(model.Snapshot, model.Devices.Where(d => d.Available).Select(d => d.Id)); }
                catch (InvalidOperationException) { return; }
                await StartAsync();
            }
        }
        finally { ticking = false; }
    }
    private string PlaybackStatus() => engine.SourceMuted
        ? "Playing · source is muted in Windows. Unmute it using Windows volume controls."
        : engine.SourceVolume <= 0 ? "Playing · Windows source volume is at zero."
        : "Playing · independent EQ, volume, delay, and routing are active";
    private void CreateTray()
    {
        using (var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/DualSync;component/Assets/DualSync.ico"))!.Stream)
        using (var embedded = new System.Drawing.Icon(stream)) trayIcon = (System.Drawing.Icon)embedded.Clone();
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "DualSync", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open DualSync", null, (_, _) => ShowWindow());
        menu.Items.Add("Start / stop audio", null, async (_, _) => { if (engine.IsRunning || wanted) await StopAsync(); else await StartAsync(); });
        foreach (var speaker in new[] { model.A, model.B })
        {
            var submenu = new Forms.ToolStripMenuItem(speaker.Title);
            submenu.DropDownItems.Add("Volume +5%", null, (_, _) => speaker.Volume = Math.Min(100, speaker.Volume + 5));
            submenu.DropDownItems.Add("Volume −5%", null, (_, _) => speaker.Volume = Math.Max(0, speaker.Volume - 5));
            submenu.DropDownItems.Add("Toggle mute", null, (_, _) => speaker.Muted = !speaker.Muted);
            menu.Items.Add(submenu);
        }
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit DualSync", null, (_, _) => { quitting = true; Close(); });
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => ShowWindow();
    }
    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        e.Cancel = true;
        if (!quitting && TrayPreference.IsChecked == true) { Hide(); return; }
        if (shutdownStarted) return;
        shutdownStarted = true;
        quitting = true; monitor.Stop(); syncMonitor.Stop(); saveTimer.Stop(); wanted = false; Save();
        while (model.Busy) await Task.Delay(25);
        try { await engine.DisposeAsync(); } catch (Exception ex) { Log(ex.ToString()); }
        tray?.Dispose(); trayIcon?.Dispose(); closed = true; _ = Dispatcher.BeginInvoke(Close);
    }
    private void UpdateProfileNames()
    {
        model.ProfileNames.Clear(); foreach (var p in settings.Profiles) model.ProfileNames.Add(p.Name);
    }
    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = ProfileName.Text.Trim();
        if (name.Length == 0) { model.Status = "Enter a name for the profile."; return; }
        var profile = (model.Snapshot with { Name = name }).Sanitize();
        settings.Profiles.RemoveAll(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        settings.Profiles.Add(profile); UpdateProfileNames(); SavedProfile.SelectedItem = profile.Name;
        Save(); model.Status = $"Saved profile: {profile.Name}";
    }
    private async void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfile.SelectedItem is not string name) return;
        var profile = settings.Profiles.First(p => p.Name == name);
        await StopAsync(); loading = true; model.Load(profile); ProfileName.Text = profile.Name; loading = false;
        await RefreshDevicesAsync(); Save(); model.Status = "Profile loaded · check your devices, then press Start audio.";
    }
    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfile.SelectedItem is not string oldName || string.IsNullOrWhiteSpace(ProfileName.Text)) return;
        var index = settings.Profiles.FindIndex(p => p.Name == oldName); var name = ProfileName.Text.Trim();
        if (settings.Profiles.Any(p => p.Name != oldName && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { model.Status = "A profile already uses that name."; return; }
        settings.Profiles[index] = (settings.Profiles[index] with { Name = name }).Sanitize(); UpdateProfileNames(); Save();
    }
    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfile.SelectedItem is not string name) return;
        settings.Profiles.RemoveAll(p => p.Name == name); UpdateProfileNames(); Save();
    }
    private async void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Reset the current audio settings? Your saved profiles will remain available.", "Reset DualSync", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        await StopAsync(); loading = true; model.Load(new()); loading = false; Save();
    }
    private void Preferences_Changed(object sender, RoutedEventArgs e) { if (!loading) Save(); }
    private void StartupPreference_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (StartupPreference.IsChecked == true)
            {
                var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Application path unavailable.");
                if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use a published DualSync executable to enable launch at sign-in.");
                key.SetValue("DualSync", "\"" + exe + "\"");
            }
            else key.DeleteValue("DualSync", false);
            model.Status = StartupPreference.IsChecked == true ? "DualSync will launch when you sign in." : "Launch at sign-in is off.";
        }
        catch (Exception ex) { StartupPreference.IsChecked = false; ShowError("Could not update startup preference", ex); }
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();
    private async void CableSetup_Click(object sender, RoutedEventArgs e)
    {
        if (!model.CanSelectDevices) return;
        wanted = false; // Do not auto-reconnect streams while a driver setup window is open.
        var setup = new CableSetupWindow { Owner = this };
        bool? accepted;
        model.Busy = true;
        try { accepted = setup.ShowDialog(); }
        finally { model.Busy = false; }
        if (accepted == true && setup.SelectedCableId is string id)
        {
            await RefreshDevicesAsync();
            model.SourceId = id;
            model.MasterModeIndex = (int)MasterMode.SourceEndpoint;
            model.IncludesVolume = false;
            Save();
            model.Status = "Cable selected in DualSync. For system audio, choose the same output in Windows Sound settings. Windows defaults have not been changed.";
        }
    }
    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder("DualSync 0.1.0-alpha.7\n").AppendLine(Environment.OSVersion.ToString()).AppendLine(model.Diagnostic);
        foreach (var device in model.Devices) report.AppendLine(device.ToString() + " | " + device.Id);
        report.AppendLine("Profile:\n" + System.Text.Json.JsonSerializer.Serialize(model.Snapshot, SettingsStore.JsonOptions));
        Clipboard.SetText(report.ToString()); model.Status = "Diagnostic report copied. It includes device names, IDs, and audio settings.";
    }
    private void DataFolder_Click(object sender, RoutedEventArgs e) { Directory.CreateDirectory(DataDirectory); Process.Start(new ProcessStartInfo("explorer.exe", DataDirectory) { UseShellExecute = true }); }
    private void Help_Click(object sender, RoutedEventArgs e) => WorkspaceTabs.SelectedItem = SetupTab;
    private void Dashboard_Click(object sender, RoutedEventArgs e) => WorkspaceTabs.SelectedItem = DashboardTab;
    private void SoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("Could not open Windows Sound settings", ex); }
    }
    private void ShowError(string title, Exception ex) { lastError = ex.Message; model.Status = title + ": " + ex.Message; Log(ex.ToString()); }
    private static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory); var path = Path.Combine(DataDirectory, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:u} {text}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
