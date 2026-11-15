using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using DualSync.App;
using DualSync.Core;

internal static class ScreenshotRenderer
{
    // Render the actual layout without code-behind, device discovery, app startup, or saved user data.
    // The status strip labels fixture devices and buffer values in every screenshot.
    public static void Render(string destination)
    {
        var source = XDocument.Load("src/DualSync.App/MainWindow.xaml");
        XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var root = source.Root!;
        root.Name = ui + "UserControl";
        root.Attribute(xaml + "Class")!.Remove();
        foreach (var name in new[] { "Title", "Icon", "Width", "Height", "MinWidth", "MinHeight", "WindowStartupLocation" }) root.Attribute(name)?.Remove();
        root.Element(ui + "Window.Resources")!.Name = ui + "UserControl.Resources";
        root.SetAttributeValue(XNamespace.Xmlns + "local", "clr-namespace:DualSync.App;assembly=DualSync");
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName is "Click" or "Checked" or "Unchecked").ToArray()) attribute.Remove();
            if (element.Name.LocalName == "ResourceDictionary" && element.Attribute("Source") is { } path)
                path.Value = "/DualSync;component/" + path.Value;
        }
        var view = (UserControl)XamlReader.Parse(source.ToString());
        var model = new ShellViewModel(new()
        {
            SourceId = "cable", MasterVolume = 0.6,
            Speakers = [new() { DeviceId = "A", Volume = 0.65, DelayMs = 23, ChannelMode = ChannelMode.Left }, new() { DeviceId = "B", Volume = 0.72, DelayMs = 27, ChannelMode = ChannelMode.Right }]
        });
        model.Devices.Add(new("cable", "CABLE Input (VB-Audio Virtual Cable)", true));
        model.Devices.Add(new("A", "Living room speaker · Bluetooth", false));
        model.Devices.Add(new("B", "Desk speaker · Bluetooth", false));
        model.A.Preset = "Music"; model.B.Preset = "Voice";
        model.A.SavedEq.Name = "Rock"; model.A.SavedEq.SaveCommand.Execute(null);
        model.B.SavedEq.Name = "Pop"; model.B.SavedEq.SaveCommand.Execute(null);
        model.DualEq.Name = "Indie · Rock + Pop"; model.DualEq.SaveCommand.Execute(null);
        model.ProfileNames.Add("My two speakers");
        model.A.UpdateQueue(576); model.B.UpdateQueue(384);
        model.Status = "UI preview · sample devices and queue values · audio is stopped";
        view.DataContext = model;
        Directory.CreateDirectory(destination);
        Layout(1180, 900);
        var autoToggle = Descendants<CheckBox>(view).Single(c => Equals(c.Content, "Auto Sync"));
        autoToggle.IsChecked = true;
        autoToggle.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.UpdateSource();
        view.UpdateLayout();
        var delaySliders = Descendants<Slider>(view).Where(s => System.Windows.Automation.AutomationProperties.GetName(s).Contains("delay milliseconds")).ToArray();
        if (!model.AutoSyncEnabled || delaySliders.Length != 2 || delaySliders.Any(s => s.IsEnabled))
            throw new InvalidOperationException("Auto Sync toggle or manual-delay bindings failed.");
        autoToggle.IsChecked = false;
        autoToggle.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.UpdateSource();
        view.UpdateLayout();
        if (model.AutoSyncEnabled || delaySliders.Any(s => !s.IsEnabled))
            throw new InvalidOperationException("Switching Auto Sync off did not unlock the delay sliders.");
        Capture("dashboard.png", 1180, 900);
        Capture("dashboard-compact.png", 860, 760);
        var tabs = (TabControl)view.FindName("WorkspaceTabs");
        tabs.SelectedIndex = 1; Capture("setup.png", 1180, 960);
        tabs.SelectedIndex = 0; Layout(1180, 1500);
        foreach (var expander in Descendants<Expander>(view).Where(e => e.Header?.ToString()?.Contains("Equalizer") == true)) expander.IsExpanded = true;
        Capture("equalizers.png", 1180, 1640);

        void Layout(int width, int height)
        {
            view.Width = width; view.Height = height;
            view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        }
        void Capture(string name, int width, int height)
        {
            Layout(width, height);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.GetFullPath(Path.Combine(destination, name));
            using var file = File.Create(path); encoder.Save(file);
            Console.WriteLine("Rendered " + path);
        }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
