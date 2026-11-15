using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualSync.Core;

public enum ChannelMode { Stereo, Left, Right, Mono, Custom }
public enum MasterMode { AppOnly, SourceEndpoint }

public sealed record SpeakerSettings
{
    public string? DeviceId { get; init; }
    public double Volume { get; init; } = 0.7;
    public bool Muted { get; init; }
    public bool EqEnabled { get; init; } = true;
    public double[] Bands { get; init; } = new double[10];
    public double DelayMs { get; init; }
    public ChannelMode ChannelMode { get; init; }
    public double LeftMix { get; init; } = 0.5;
    public double RightMix { get; init; } = 0.5;

    public SpeakerSettings Sanitize() => this with
    {
        Volume = Numbers.Clamp(Volume, 0, 1, 0.7),
        DelayMs = Numbers.Clamp(DelayMs, 0, 500, 0),
        Bands = Enumerable.Range(0, 10).Select(i => Numbers.Clamp(Bands is not null && i < Bands.Length ? Bands[i] : 0, -12, 12, 0)).ToArray(),
        LeftMix = Numbers.Clamp(LeftMix, 0, 1, 0.5), RightMix = Numbers.Clamp(RightMix, 0, 1, 0.5),
        ChannelMode = Enum.IsDefined(ChannelMode) ? ChannelMode : ChannelMode.Stereo
    };
}

public sealed record AudioProfile
{
    public string Name { get; init; } = "My speakers";
    public string? SourceId { get; init; }
    public double MasterVolume { get; init; } = 0.7;
    public MasterMode MasterMode { get; init; } = MasterMode.SourceEndpoint;
    // True only after the user has verified that source volume already scales loopback samples.
    public bool LoopbackIncludesEndpointVolume { get; init; }
    public bool AutoSyncEnabled { get; init; }
    public SpeakerSettings[] Speakers { get; init; } = [new(), new()];

    public AudioProfile Sanitize() => this with
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "My speakers" : Name.Trim()[..Math.Min(Name.Trim().Length, 80)],
        MasterVolume = Numbers.Clamp(MasterVolume, 0, 1, 0.7),
        MasterMode = Enum.IsDefined(MasterMode) ? MasterMode : MasterMode.AppOnly,
        Speakers = Enumerable.Range(0, 2).Select(i => (Speakers is not null && i < Speakers.Length ? Speakers[i] : null)?.Sanitize() ?? new SpeakerSettings()).ToArray()
    };
}

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 3;
    public AudioProfile Current { get; init; } = new();
    public List<AudioProfile> Profiles { get; init; } = [];
    public List<EqPreset> EqPresets { get; init; } = [];
    public List<DualEqPreset> DualEqPresets { get; init; } = [];
    public bool MinimizeToTray { get; init; }
    public bool StartMinimized { get; init; }
    public AppSettings Sanitize() => this with
    {
        Current = (Current ?? new()).Sanitize(),
        Profiles = (Profiles ?? []).Where(p => p is not null).Take(100).Select(p => p.Sanitize()).ToList(),
        EqPresets = new EqPresetLibrary(EqPresets).Singles.ToList(),
        DualEqPresets = new EqPresetLibrary(dualPresets: DualEqPresets).Duals.ToList()
    };
}

public static class Numbers
{
    public static double Clamp(double n, double min, double max, double fallback) => double.IsFinite(n) ? Math.Clamp(n, min, max) : fallback;
    public static double DbToGain(double db) => Math.Pow(10, db / 20);
}

public static class Presets
{
    public static readonly double[] Frequencies = [31.25, 62.5, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
    public static readonly string[] Names = ["Balanced", "Music", "Movies", "Voice"];
    public static double[] Get(string name) => name switch
    {
        "Music" => [2, 2, 1, 0, -1, -1, 0, 1, 2, 1],
        "Movies" => [3, 3, 1, 0, -1, 1, 2, 1, 0, 0],
        "Voice" => [-6, -4, -2, 0, 1, 2, 3, 2, 0, -2],
        _ => new double[10]
    };
}

public sealed class SettingsStore(string path)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    public string? LoadWarning { get; private set; }
    public AppSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(path)) return new();
        try
        {
            var value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? throw new JsonException("Empty settings");
            if (value.SchemaVersion == 1)
            {
                // Alpha 1's default bypassed Windows volume. Upgrade its app-only profiles
                // without modifying any Windows endpoint settings or speaker parameters.
                static AudioProfile Upgrade(AudioProfile? p)
                {
                    p ??= new();
                    return p.MasterMode == MasterMode.AppOnly
                        ? p with { MasterMode = MasterMode.SourceEndpoint, LoopbackIncludesEndpointVolume = false } : p;
                }
                value = value with { SchemaVersion = 2, Current = Upgrade(value.Current), Profiles = (value.Profiles ?? []).Select(Upgrade).ToList() };
            }
            if (value.SchemaVersion == 2) value = value with { SchemaVersion = 3 };
            if (value.SchemaVersion != 3) throw new JsonException("Unsupported settings version");
            return value.Sanitize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadWarning = "Saved settings could not be loaded. Defaults are in use. " + ex.Message;
            // Keep the original for recovery; the next explicit edit can save a fresh configuration.
            try { File.Copy(path, path + ".recovery-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false); }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException) { }
            return new();
        }
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Sanitize(), JsonOptions));
        File.Move(temp, path, true);
    }
}

public static class RouteValidation
{
    public static void Validate(AudioProfile profile, IEnumerable<string> available)
    {
        var ids = available.ToHashSet(StringComparer.Ordinal);
        if (profile.SourceId is null || !ids.Contains(profile.SourceId)) throw new InvalidOperationException("Choose a connected source device.");
        if (profile.Speakers.Length < 2) throw new InvalidOperationException("Choose two speakers.");
        foreach (var speaker in profile.Speakers)
        {
            if (speaker.DeviceId is null || !ids.Contains(speaker.DeviceId)) throw new InvalidOperationException("Choose a connected device for each speaker.");
            if (speaker.DeviceId == profile.SourceId) throw new InvalidOperationException("The source must be different from both speakers to prevent feedback. See Setup help.");
        }
        if (profile.Speakers.Select(s => s.DeviceId).Distinct().Count() != profile.Speakers.Length)
            throw new InvalidOperationException("Select different devices for Speaker A and Speaker B.");
    }
}
