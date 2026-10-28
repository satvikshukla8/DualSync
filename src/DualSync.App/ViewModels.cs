using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DualSync.Audio;
using DualSync.Core;

namespace DualSync.App;

public sealed class ActionCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Notify(property); return true;
    }
}

public sealed class BandViewModel(string label, double value) : Observable
{
    private double gain = value;
    public string Label { get; } = label;
    public double Gain { get => gain; set { if (Set(ref gain, Math.Round(value, 1))) Changed?.Invoke(); } }
    public event Action? Changed;
}

public sealed class SpeakerViewModel : Observable
{
    private SpeakerSettings settings;
    private bool loading;
    private string? preset;
    private string telemetry = "Stopped";
    private double meter;
    private double? queueMilliseconds;
    public sealed record ChannelOption(ChannelMode Value, string Label)
    {
        public override string ToString() => Label;
    }
    public ChannelOption[] ChannelOptions { get; } =
    [
        new(ChannelMode.Stereo, "Stereo · left and right"),
        new(ChannelMode.Left, "Left only"),
        new(ChannelMode.Right, "Right only"),
        new(ChannelMode.Mono, "Mono · blend left and right"),
        new(ChannelMode.Custom, "Custom left / right mix")
    ];
    public string Title { get; }
    public string Subtitle { get; }
    public ObservableCollection<AudioDevice> Devices { get; }
    public ObservableCollection<BandViewModel> Bands { get; }
    public ICommand DelayStepCommand { get; }
    public ICommand ResetEqCommand { get; }
    public string[] PresetNames => Presets.Names;
    public ChannelMode[] ChannelModes => Enum.GetValues<ChannelMode>();
    public event Action? Changed;
    public SpeakerViewModel(string title, string subtitle, SpeakerSettings initial, ObservableCollection<AudioDevice> devices)
    {
        Title = title; Subtitle = subtitle; settings = initial.Sanitize(); Devices = devices;
        string[] labels = ["31", "63", "125", "250", "500", "1k", "2k", "4k", "8k", "16k"];
        Bands = new(labels.Select((label, i) => new BandViewModel(label, settings.Bands[i])));
        foreach (var band in Bands) band.Changed += BandsChanged;
        DelayStepCommand = new ActionCommand(parameter => { if (int.TryParse(parameter?.ToString(), out var step)) AdjustDelay(step); });
        ResetEqCommand = new ActionCommand(_ => Preset = "Balanced");
    }
    public SpeakerSettings Snapshot => settings with { Bands = Bands.Select(b => b.Gain).ToArray() };
    public string? DeviceId { get => settings.DeviceId; set { if (settings.DeviceId == value) return; settings = settings with { DeviceId = value }; Change(); } }
    public double Volume { get => settings.Volume * 100; set { settings = settings with { Volume = value / 100 }; Change(); } }
    public bool Muted { get => settings.Muted; set { settings = settings with { Muted = value }; Change(); } }
    public bool EqEnabled { get => settings.EqEnabled; set { settings = settings with { EqEnabled = value }; Change(); } }
    public double DelayMs
    {
        get => settings.DelayMs;
        set
        {
            var delay = Math.Round(Numbers.Clamp(value, 0, 500, 0), 1);
            if (settings.DelayMs == delay) return;
            settings = settings with { DelayMs = delay }; Change(); NotifyTiming();
        }
    }
    public void AdjustDelay(int milliseconds) => DelayMs += milliseconds;
    public ChannelMode ChannelMode
    {
        get => settings.ChannelMode;
        set
        {
            settings = settings with { ChannelMode = value }; Change();
            Notify(nameof(CustomMixEnabled)); Notify(nameof(RoutingDescription));
        }
    }
    public bool CustomMixEnabled => ChannelMode == ChannelMode.Custom;
    public string RoutingDescription => ChannelMode switch
    {
        ChannelMode.Left => "The left source channel plays through this speaker.",
        ChannelMode.Right => "The right source channel plays through this speaker.",
        ChannelMode.Mono => "Left and right are blended equally into mono.",
        ChannelMode.Custom => "Set each source channel's contribution to the mono mix.",
        _ => "Both source channels keep their original stereo positions."
    };
    public double LeftMix { get => settings.LeftMix; set { settings = settings with { LeftMix = value }; Change(); } }
    public double RightMix { get => settings.RightMix; set { settings = settings with { RightMix = value }; Change(); } }
    public string? Preset
    {
        get => preset;
        set
        {
            if (value is null) { preset = null; Notify(); return; }
            loading = true; var gains = Presets.Get(value);
            for (var i = 0; i < 10; i++) Bands[i].Gain = gains[i];
            loading = false; preset = value; Notify(); Change(nameof(Snapshot));
        }
    }
    public string Telemetry { get => telemetry; set => Set(ref telemetry, value); }
    public double Meter { get => meter; set => Set(ref meter, value); }
    public string QueueDisplay => queueMilliseconds is double queue ? $"{queue:0.0} ms" : "—";
    public string AddedDelayDisplay => $"{DelayMs:0.0} ms";
    public string FinalBufferDisplay => queueMilliseconds is double queue ? $"{queue + DelayMs:0.0} ms" : "—";
    public bool CanDecreaseDelay => DelayMs > 0;
    public bool CanIncreaseDelay => DelayMs < 500;
    public void UpdateQueue(int? frames)
    {
        queueMilliseconds = frames is int value ? Math.Max(0, value) * 1000.0 / AudioEngine.SampleRate : null;
        NotifyTiming();
    }
    private void NotifyTiming()
    {
        Notify(nameof(QueueDisplay)); Notify(nameof(AddedDelayDisplay)); Notify(nameof(FinalBufferDisplay));
        Notify(nameof(CanDecreaseDelay)); Notify(nameof(CanIncreaseDelay));
    }
    public string HeadroomText => "Peak protection reduces gain only if the audio would overload. No fixed EQ volume cut.";
    private void BandsChanged() { if (loading) return; Preset = null; Change(nameof(Snapshot)); }
    private void Change([CallerMemberName] string? property = null)
    {
        Notify(property); Notify(nameof(Snapshot)); Notify(nameof(HeadroomText));
        if (!loading) Changed?.Invoke();
    }
    public void Load(SpeakerSettings value)
    {
        loading = true; settings = value.Sanitize();
        for (var i = 0; i < 10; i++) Bands[i].Gain = settings.Bands[i];
        preset = null; loading = false; Notify(string.Empty);
    }
}

public sealed class ShellViewModel : Observable
{
    private AudioProfile profile;
    private double master;
    private string status = "Ready · choose a source and two speakers";
    private string diagnostic = "Start audio to view buffer and signal diagnostics.";
    private bool running, busy;
    public ObservableCollection<AudioDevice> Devices { get; } = [];
    public ObservableCollection<string> ProfileNames { get; } = [];
    public SpeakerViewModel A { get; }
    public SpeakerViewModel B { get; }
    public event Action<string>? Changed;
    public ShellViewModel(AudioProfile initial)
    {
        profile = initial.Sanitize(); master = profile.MasterVolume * 100;
        A = new("Speaker A", "Your first output", profile.Speakers[0], Devices);
        B = new("Speaker B", "Your second output", profile.Speakers[1], Devices);
        A.Changed += () => Changed?.Invoke("Speaker"); B.Changed += () => Changed?.Invoke("Speaker");
    }
    public AudioProfile Snapshot => profile with { MasterVolume = master / 100, Speakers = [A.Snapshot, B.Snapshot] };
    public string? SourceId { get => profile.SourceId; set { if (profile.SourceId == value) return; profile = profile with { SourceId = value }; Notify(); Changed?.Invoke(nameof(SourceId)); } }
    public double Master { get => master; set { if (Set(ref master, Math.Round(value, 1))) Changed?.Invoke(nameof(Master)); } }
    public int MasterModeIndex
    {
        get => (int)profile.MasterMode;
        set { profile = profile with { MasterMode = (MasterMode)Math.Clamp(value, 0, 1) }; Notify(); Changed?.Invoke(nameof(MasterModeIndex)); }
    }
    public bool IncludesVolume { get => profile.LoopbackIncludesEndpointVolume; set { profile = profile with { LoopbackIncludesEndpointVolume = value }; Notify(); Changed?.Invoke(nameof(IncludesVolume)); } }
    public string Status { get => status; set => Set(ref status, value); }
    public string Diagnostic { get => diagnostic; set => Set(ref diagnostic, value); }
    public bool Running { get => running; set { Set(ref running, value); Notify(nameof(CanSelectDevices)); Notify(nameof(PlaybackLabel)); } }
    public bool Busy { get => busy; set { Set(ref busy, value); Notify(nameof(CanSelectDevices)); Notify(nameof(CanOperate)); } }
    public bool CanSelectDevices => !running && !busy;
    public bool CanOperate => !busy;
    public string PlaybackLabel => Running ? "Stop audio" : "Start audio";
    public void MirrorSourceVolume(double value) { master = value * 100; Notify(nameof(Master)); }
    public void Load(AudioProfile value)
    {
        profile = value.Sanitize(); master = profile.MasterVolume * 100;
        A.Load(profile.Speakers[0]); B.Load(profile.Speakers[1]); Notify(string.Empty);
    }
}
