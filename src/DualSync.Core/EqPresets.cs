namespace DualSync.Core;

public sealed record EqSettings
{
    public bool Enabled { get; init; } = true;
    public double[] Bands { get; init; } = new double[10];

    public EqSettings Sanitize() => this with
    {
        Bands = new SpeakerSettings { Bands = Bands }.Sanitize().Bands
    };

    public static EqSettings Capture(SpeakerSettings speaker) => new EqSettings
    {
        Enabled = speaker.EqEnabled, Bands = speaker.Bands
    }.Sanitize();

    public SpeakerSettings ApplyTo(SpeakerSettings speaker)
    {
        var eq = Sanitize();
        return speaker with { EqEnabled = eq.Enabled, Bands = eq.Bands };
    }
}

public sealed record EqPreset
{
    public string Name { get; init; } = "My EQ";
    public EqSettings Eq { get; init; } = new();
}

public sealed record DualEqPreset
{
    public string Name { get; init; } = "My dual EQ";
    public EqSettings A { get; init; } = new();
    public EqSettings B { get; init; } = new();
}

// Separate libraries allow the same name for a single curve and a pair of curves.
// Every capture, load, and snapshot copies the bands so later edits cannot alter a saved preset.
public sealed class EqPresetLibrary
{
    public const int MaximumPresets = 100;
    public const int MaximumNameLength = 80;
    private readonly List<EqPreset> singles = [];
    private readonly List<DualEqPreset> duals = [];
    public EqPreset[] Singles => singles.Select(p => p with { Eq = p.Eq.Sanitize() }).ToArray();
    public DualEqPreset[] Duals => duals.Select(p => p with { A = p.A.Sanitize(), B = p.B.Sanitize() }).ToArray();

    public EqPresetLibrary(IEnumerable<EqPreset>? singlePresets = null, IEnumerable<DualEqPreset>? dualPresets = null)
    {
        foreach (var p in singlePresets ?? [])
        {
            if (p is null || !TryName(p.Name, out var name) || singles.Any(x => Same(x.Name, name))) continue;
            singles.Add(p with { Name = name, Eq = (p.Eq ?? new()).Sanitize() });
            if (singles.Count == MaximumPresets) break;
        }
        foreach (var p in dualPresets ?? [])
        {
            if (p is null || !TryName(p.Name, out var name) || duals.Any(x => Same(x.Name, name))) continue;
            duals.Add(p with { Name = name, A = (p.A ?? new()).Sanitize(), B = (p.B ?? new()).Sanitize() });
            if (duals.Count == MaximumPresets) break;
        }
    }

    public string SaveSingle(string name, SpeakerSettings speaker)
    {
        name = ValidateName(name);
        var index = singles.FindIndex(p => Same(p.Name, name));
        var preset = new EqPreset { Name = name, Eq = EqSettings.Capture(speaker) };
        if (index >= 0) singles[index] = preset;
        else { CheckCapacity(singles.Count); singles.Add(preset); }
        return name;
    }

    public string SaveDual(string name, SpeakerSettings a, SpeakerSettings b)
    {
        name = ValidateName(name);
        var index = duals.FindIndex(p => Same(p.Name, name));
        var preset = new DualEqPreset { Name = name, A = EqSettings.Capture(a), B = EqSettings.Capture(b) };
        if (index >= 0) duals[index] = preset;
        else { CheckCapacity(duals.Count); duals.Add(preset); }
        return name;
    }

    public EqSettings LoadSingle(string name) => singles.First(p => Same(p.Name, name)).Eq.Sanitize();
    public DualEqPreset LoadDual(string name)
    {
        var p = duals.First(p => Same(p.Name, name));
        return p with { A = p.A.Sanitize(), B = p.B.Sanitize() };
    }
    public void DeleteSingle(string name) => singles.RemoveAll(p => Same(p.Name, name));
    public void DeleteDual(string name) => duals.RemoveAll(p => Same(p.Name, name));

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
    private static bool TryName(string? value, out string name)
    {
        name = value?.Trim() ?? "";
        return name.Length is > 0 and <= MaximumNameLength;
    }
    private static string ValidateName(string name) => TryName(name, out var valid) ? valid
        : throw new ArgumentException($"Enter an EQ preset name of 1–{MaximumNameLength} characters.");
    private static void CheckCapacity(int count)
    {
        if (count >= MaximumPresets) throw new InvalidOperationException($"This EQ library is full ({MaximumPresets} presets). Replace or delete a preset first.");
    }
}
