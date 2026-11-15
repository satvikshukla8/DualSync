using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using DualSync.App;
using DualSync.Core;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Auto Sync replaces manual delays together and preserves every other setting", () =>
            {
                var shell = CreateShell(); shell.AutoSyncEnabled = true; shell.Running = true;
                var before = shell.Snapshot; var updates = 0;
                shell.Changed += property =>
                {
                    if (property != "AutoSyncDelay") return;
                    updates++; Check(shell.A.DelayMs == 8 && shell.B.DelayMs == 0, "Audio observed a half-applied delay pair");
                };
                for (var i = 0; i <= 80; i++) shell.ObserveAutoSync(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 576, 960));
                Check(updates == 1, "A stable window did not apply exactly one complete update");
                Check(SameSpeaker(shell.A.Snapshot with { DelayMs = before.Speakers[0].DelayMs }, before.Speakers[0])
                    && SameSpeaker(shell.B.Snapshot with { DelayMs = before.Speakers[1].DelayMs }, before.Speakers[1]), "Auto Sync changed EQ, gain, routing, mute, or devices");
                Check(shell.AutoSyncDetails.Contains("12.0") && shell.AutoSyncDetails.Contains("20.0"), "Average results are not shown");
                shell.AutoSyncEnabled = false;
                Check(shell.A.ManualDelayEnabled && shell.B.ManualDelayEnabled && shell.A.DelayMs == 8 && shell.B.DelayMs == 0, "Switching off changed delays or left manual controls locked");
                shell.A.AdjustDelay(1); Check(shell.A.DelayMs == 9, "Manual fine adjustment did not resume");
            }),
            ("Auto Sync ignores small jitter and manual step commands while enabled", () =>
            {
                var shell = CreateShell(); shell.A.DelayMs = 8; shell.B.DelayMs = 0;
                shell.AutoSyncEnabled = true; shell.Running = true; var updates = 0;
                shell.Changed += property => { if (property == "AutoSyncDelay") updates++; };
                shell.A.DelayStepCommand.Execute("1");
                Check(shell.A.DelayMs == 8 && !shell.A.CanIncreaseDelay && !shell.A.CanDecreaseDelay && !shell.B.ManualDelayEnabled, "Manual steps were allowed in automatic mode");
                for (var i = 0; i <= 280; i++) shell.ObserveAutoSync(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 576, 984));
                Check(updates == 0 && shell.A.DelayMs == 8 && shell.B.DelayMs == 0, "Sub-millisecond jitter moved the delay");
            }),
            ("Auto Sync cancellation and stop/restart discard partial measurements", () =>
            {
                var shell = CreateShell(); shell.AutoSyncEnabled = true; shell.Running = true;
                for (var i = 0; i <= 60; i++) shell.ObserveAutoSync(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 480, 960));
                shell.AutoSyncEnabled = false;
                for (var i = 61; i <= 100; i++) shell.ObserveAutoSync(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 480, 960));
                Check(shell.A.DelayMs == 23 && shell.B.DelayMs == 41, "Cancelled measurement changed manual delays");
                shell.AutoSyncEnabled = true; shell.Running = false;
                for (var i = 0; i <= 80; i++) shell.ObserveAutoSync(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 480, 960));
                Check(shell.A.DelayMs == 23, "Stopped playback applied a measurement");
                shell.Running = true;
                for (var i = 0; i < 80; i++) shell.ObserveAutoSync(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 480, 960));
                Check(shell.A.DelayMs == 23 && shell.AutoSyncProgress < 100, "Restart reused an old window");
                shell.ObserveAutoSync(new(TimeSpan.FromSeconds(4), 80 * 2400L, 480, 960));
                Check(shell.A.DelayMs == 10 && shell.B.DelayMs == 0, "Restart did not complete a fresh window");
            }),
            ("Full setup loads restore Auto Sync mode while EQ preset loads preserve it", () =>
            {
                var shell = CreateShell(); shell.A.SavedEq.Name = "Rock"; shell.A.SavedEq.SaveCommand.Execute(null);
                shell.Load(shell.Snapshot with { AutoSyncEnabled = true });
                Check(shell.AutoSyncEnabled && !shell.A.ManualDelayEnabled && !shell.B.ManualDelayEnabled, "Full setup did not restore automatic mode");
                shell.A.SavedEq.LoadCommand.Execute(null);
                Check(shell.AutoSyncEnabled, "EQ load changed automatic mode");
                shell.Load(new()); Check(!shell.AutoSyncEnabled && shell.A.ManualDelayEnabled, "Manual full setup did not unlock delay controls");
            }),
            ("Single EQ picker saves for either speaker and applies only on Load", () =>
            {
                var shell = CreateShell(); var originalB = shell.B.Snapshot;
                shell.A.Preset = "Music"; shell.A.EqEnabled = false;
                shell.A.SavedEq.Name = "Rock"; shell.A.SavedEq.SaveCommand.Execute(null);
                Check(shell.B.SavedEq.Names.Contains("Rock"), "Preset not available on the other speaker");
                shell.B.SavedEq.SelectedName = "Rock";
                Check(SameSpeaker(shell.B.Snapshot, originalB), "Selecting a preset applied it before Load");
                shell.B.SavedEq.LoadCommand.Execute(null);
                Check(shell.B.Snapshot.Bands.SequenceEqual(Presets.Get("Music")) && !shell.B.EqEnabled, "Cross-speaker load failed");
                Check(SameNonEq(shell.B.Snapshot, originalB), "Loading changed volume, delay, or routing");
                shell.A.Bands[0].Gain = -9;
                shell.B.SavedEq.LoadCommand.Execute(null);
                Check(shell.B.Bands[0].Gain == 2, "Later slider changes altered saved EQ");
            }),
            ("Dual EQ picker restores each side in one audio update", () =>
            {
                var shell = CreateShell(); shell.A.Preset = "Music"; shell.B.Preset = "Voice"; shell.B.EqEnabled = false;
                shell.DualEq.Name = "Indie"; shell.DualEq.SaveCommand.Execute(null);
                shell.A.Preset = shell.B.Preset = "Balanced"; shell.B.EqEnabled = true;
                var a = shell.A.Snapshot; var b = shell.B.Snapshot; var updates = 0;
                shell.Changed += _ =>
                {
                    updates++;
                    Check(shell.A.Snapshot.Bands.SequenceEqual(Presets.Get("Music")) && shell.B.Snapshot.Bands.SequenceEqual(Presets.Get("Voice")), "Audio update observed a half-loaded pair");
                };
                shell.DualEq.LoadCommand.Execute(null);
                Check(updates == 1 && !shell.B.EqEnabled && shell.A.EqEnabled, "Dual load did not batch both sides");
                Check(SameNonEq(shell.A.Snapshot, a) && SameNonEq(shell.B.Snapshot, b), "Dual EQ changed non-EQ settings");
            }),
            ("Preset replacements refresh both pickers while keeping name drafts", () =>
            {
                var shell = CreateShell(); shell.A.SavedEq.Name = "Rock"; shell.A.SavedEq.SaveCommand.Execute(null);
                shell.B.SavedEq.SelectedName = "Rock"; shell.B.SavedEq.Name = "My next EQ";
                shell.A.Preset = "Voice"; shell.A.SavedEq.Name = "rock";
                Check(shell.A.SavedEq.SaveLabel == "Replace EQ", "Replacement is not labeled");
                shell.A.SavedEq.SaveCommand.Execute(null);
                Check(shell.B.SavedEq.Name == "My next EQ" && shell.B.SavedEq.HasSelection && shell.A.SavedEq.Names.Count == 1, "Refresh lost draft or duplicated names");
                shell.B.SavedEq.LoadCommand.Execute(null);
                Check(shell.B.Bands[0].Gain == -6, "Other picker loaded an outdated replacement");
                var current = shell.A.Snapshot; shell.A.SavedEq.DeleteCommand.Execute(null);
                Check(shell.B.SavedEq.Names.Count == 0 && !shell.B.SavedEq.HasSelection && SameSpeaker(current, shell.A.Snapshot), "Delete affected current EQ or left stale selection");
            }),
            ("Full setup loads leave saved EQ libraries available", () =>
            {
                var shell = CreateShell(); shell.A.SavedEq.Name = "Rock"; shell.A.SavedEq.SaveCommand.Execute(null);
                shell.DualEq.Name = "Indie"; shell.DualEq.SaveCommand.Execute(null);
                shell.Load(new() { Name = "Another room" });
                Check(shell.A.SavedEq.Names.Contains("Rock") && shell.DualEq.Names.Contains("Indie"), "Setup load discarded the EQ libraries");
                var restored = new ShellViewModel(shell.Snapshot, shell.EqLibrary.Singles, shell.EqLibrary.Duals);
                restored.DualEq.SelectedName = "Indie"; restored.DualEq.LoadCommand.Execute(null);
                Check(restored.A.SavedEq.Names.Contains("Rock"), "Restarted view model lost single EQ");
            }),
            ("Compiled EQ templates bind save and load controls at compact widths", () =>
            {
                // Initialize theme resources only: no app startup, devices, tray, or native windows.
                var app = new DualSync.App.App(); app.InitializeComponent();
                var resources = new ResourceDictionary { Source = new Uri("/DualSync;component/Dashboard.xaml", UriKind.Relative) };
                var shell = CreateShell();
                var panel = new ContentControl { Content = shell.A, ContentTemplate = (DataTemplate)resources["EqPanel"], Width = 340 };
                panel.Measure(new Size(340, 1800)); panel.Arrange(new Rect(0, 0, 340, panel.DesiredSize.Height)); panel.UpdateLayout();
                var name = Descendants<TextBox>(panel).Single();
                name.Text = "Template EQ"; name.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                var save = Descendants<Button>(panel).Single(b => Equals(b.Content, "Save EQ"));
                Check(save.IsEnabled && shell.A.SavedEq.Name == "Template EQ", "Name binding failed");
                save.Command.Execute(save.CommandParameter); panel.UpdateLayout();
                Check(shell.EqLibrary.Singles.Single().Name == "Template EQ", "Save binding failed");
                var load = Descendants<Button>(panel).Single(b => Equals(b.Content, "Load EQ"));
                Check(load.IsEnabled && shell.A.SavedEq.HasSelection, "Load availability binding failed");
                Check(Descendants<FrameworkElement>(panel).All(e => double.IsFinite(e.ActualWidth)), "Template did not lay out");
                var band = Descendants<Slider>(panel).First();
                band.Value = 5.5; panel.UpdateLayout();
                var track = Descendants<Track>(band).Single();
                Check(shell.A.Bands[0].Gain == 5.5 && track.Value == 5.5 && track.Minimum == -12 && track.Maximum == 12, "Styled EQ slider lost its band value or range binding");
                if (args.Contains("--render")) RenderPreview(shell, resources);
                var screenshotArg = Array.IndexOf(args, "--screenshots");
                if (screenshotArg >= 0) ScreenshotRenderer.Render(args[screenshotArg + 1]);
                app.Shutdown();
            })
        };
        var failed = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); }
            catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + test.Name + "\n" + ex); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} app tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static ShellViewModel CreateShell() => new(new()
    {
        SourceId = "source", MasterVolume = 0.4,
        Speakers =
        [
            new() { DeviceId = "A", Volume = 0.31, DelayMs = 23, ChannelMode = ChannelMode.Left },
            new() { DeviceId = "B", Volume = 0.64, DelayMs = 41, Muted = true, ChannelMode = ChannelMode.Custom, LeftMix = 0.2, RightMix = 0.8 }
        ]
    });
    private static void RenderPreview(ShellViewModel shell, ResourceDictionary resources)
    {
        shell.DualEq.Name = "Indie · A Rock + B Pop"; shell.DualEq.SaveCommand.Execute(null);
        var root = new StackPanel { Width = 800, Background = (Brush)Application.Current.Resources["BackgroundBrush"] };
        System.Windows.Documents.TextElement.SetForeground(root, (Brush)Application.Current.Resources["TextBrush"]);
        root.Children.Add(new TextBlock { Text = "Dual EQ presets · save both speakers together", Foreground = Brushes.White, Margin = new Thickness(20), FontSize = 18 });
        root.Children.Add(new ContentControl { Content = shell.DualEq, ContentTemplate = (DataTemplate)resources["SavedEqPicker"], Margin = new Thickness(20, 0, 36, 20) });
        var pair = new Grid(); pair.ColumnDefinitions.Add(new()); pair.ColumnDefinitions.Add(new());
        var a = new ContentControl { Content = shell.A, ContentTemplate = (DataTemplate)resources["SpeakerCard"] };
        var b = new ContentControl { Content = shell.B, ContentTemplate = (DataTemplate)resources["SpeakerCard"] };
        Grid.SetColumn(b, 1); pair.Children.Add(a); pair.Children.Add(b); root.Children.Add(pair);
        Arrange();
        foreach (var expander in Descendants<Expander>(root)) expander.IsExpanded = true;
        Arrange();
        var bitmap = new RenderTargetBitmap(800, (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.GetFullPath(Path.Combine("artifacts", "eq-presets-preview.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path); encoder.Save(file);
        Console.WriteLine("Rendered compact EQ preview: " + path);
        void Arrange()
        {
            root.Measure(new Size(800, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, 800, root.DesiredSize.Height)); root.UpdateLayout();
        }
    }
    private static bool SameNonEq(SpeakerSettings a, SpeakerSettings b) => (a with { Bands = b.Bands, EqEnabled = b.EqEnabled }) == b;
    private static bool SameSpeaker(SpeakerSettings a, SpeakerSettings b) => SameNonEq(a, b) && a.EqEnabled == b.EqEnabled && a.Bands.SequenceEqual(b.Bands);
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
