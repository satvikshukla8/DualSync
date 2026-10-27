using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DualSync.Core;

namespace DualSync.App;

public sealed class EqCurve : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(SpeakerSettings), typeof(EqCurve), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public SpeakerSettings? Settings { get => (SpeakerSettings?)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = Math.Max(1, ActualWidth - 40); var h = Math.Max(1, ActualHeight - 22);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16, 23, 33)), null, new(0, 0, ActualWidth, ActualHeight), 10, 10);
        for (var db = -24; db <= 24; db += 12)
        {
            var y = 8 + (24 - db) / 48.0 * (h - 16);
            dc.DrawLine(new(new SolidColorBrush(Color.FromRgb(47, 60, 76)), 1), new(34, y), new(w + 34, y));
            Text(dc, db.ToString(CultureInfo.InvariantCulture), 4, y - 7);
        }
        foreach (var frequency in new[] { 31, 125, 500, 2000, 8000, 20000 })
        {
            var x = 34 + Math.Log(frequency / 20.0) / Math.Log(1000) * w;
            Text(dc, frequency >= 1000 ? $"{frequency / 1000}k" : frequency.ToString(CultureInfo.InvariantCulture), x - 10, h + 1);
        }
        var s = Settings; if (s is null) return;
        var filters = s.Bands.Select((gain, i) => BiquadCoefficients.Peak(48000, Presets.Frequencies[i], gain)).ToArray();
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i <= 180; i++)
            {
                var frequency = 20 * Math.Pow(1000, i / 180.0);
                var db = s.EqEnabled ? filters.Sum(c => 20 * Math.Log10(c.Magnitude(48000, frequency))) : 0;
                var point = new Point(34 + i / 180.0 * w, 8 + (24 - Math.Clamp(db, -24, 24)) / 48 * (h - 16));
                if (i == 0) context.BeginFigure(point, false, false); else context.LineTo(point, true, false);
            }
        }
        dc.DrawGeometry(null, new(new SolidColorBrush(Color.FromRgb(97, 219, 202)), 2), geometry);
    }
    private void Text(DrawingContext dc, string text, double x, double y) => dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, new SolidColorBrush(Color.FromRgb(151, 168, 190)), VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
}
