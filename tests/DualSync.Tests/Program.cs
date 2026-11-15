using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DualSync.Core;

var tests = new (string Name, Action Run)[]
{
    ("Auto Sync averages paired queues for three seconds after warmup", () =>
    {
        var sync = new BufferAutoSync(); BufferSyncReading reading = default;
        for (var i = 0; i <= 80; i++)
        {
            reading = sync.Observe(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, i % 2 == 0 ? 480 : 960, 1440));
            if (i < 80) Check(reading.Measurement is null, "Delay applied before a complete measurement window");
        }
        var result = reading.Measurement ?? throw new InvalidOperationException("No stable measurement");
        Near(result.AverageA, (31 * 10.0 + 30 * 20.0) / 61, 0.000001); Near(result.AverageB, 30, 0);
        Near(result.DelayA, 15.1, 0); Near(result.DelayB, 0, 0);
        Check(result.Samples == 61 && reading.Progress == 100, "Wrong paired sample count or progress");
    }),
    ("Auto Sync delays the shorter queue and respects equal and maximum queues", () =>
    {
        foreach (var (a, b, delayA, delayB) in new[] { (960, 480, 0.0, 10.0), (0, 24000, 500.0, 0.0), (24000, 0, 0.0, 500.0), (480, 480, 0.0, 0.0) })
        {
            var sync = new BufferAutoSync(); BufferSyncMeasurement? result = null;
            for (var i = 0; i <= 80; i++) result = sync.Observe(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, a, b)).Measurement ?? result;
            Check(result is not null, "No result for a valid queue pair");
            Near(result!.DelayA, delayA, 0); Near(result.DelayB, delayB, 0);
            Near(result.AverageA + result.DelayA, result.AverageB + result.DelayB, 0.1);
        }
    }),
    ("Auto Sync discards partial windows on pauses, dropouts, restarts, and sampling gaps", () =>
    {
        foreach (var disruption in new[] { "pause", "missing", "dropped", "restart", "gap" })
        {
            var sync = new BufferAutoSync();
            for (var i = 0; i <= 60; i++) sync.Observe(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 480, 960));
            var at = disruption == "gap" ? 4.0 : 3.05;
            var captured = disruption == "pause" ? 60 * 2400L : disruption == "restart" ? 0 : 61 * 2400L;
            var missing = disruption == "missing" ? 48 : 0; var dropped = disruption == "dropped" ? 48 : 0;
            var disrupted = sync.Observe(new(TimeSpan.FromSeconds(at), captured, 480, 960, MissingA: missing, DroppedB: dropped));
            Check(disrupted.Measurement is null, "Disruption produced a measurement");
            BufferSyncMeasurement? result = null;
            for (var i = 1; i <= 80; i++)
            {
                var reading = sync.Observe(new(TimeSpan.FromSeconds(at) + TimeSpan.FromMilliseconds(i * 50), captured + i * 2400L, 480, 960, MissingA: missing, DroppedB: dropped));
                if (i < 80) Check(reading.Measurement is null, "Partial pre-disruption window survived");
                result = reading.Measurement ?? result;
            }
            Check(result is not null, "Did not recover after " + disruption);
        }
    }),
    ("Auto Sync rejects undersampled and invalid windows", () =>
    {
        var sync = new BufferAutoSync();
        for (var i = 0; i <= 100; i++)
            Check(sync.Observe(new(TimeSpan.FromMilliseconds(i * 100), i * 4800L, 480, 960)).Measurement is null, "Undersampled window was accepted");
        foreach (var bad in new[] { new BufferSyncSample(TimeSpan.Zero, 0, -1, 0), new(TimeSpan.Zero, 0, 0, 24001), new(TimeSpan.Zero, -1, 0, 0), new(TimeSpan.Zero, 0, 0, 0, MissingA: -1) })
            Check(sync.Observe(bad).State == BufferSyncState.Unstable, "Invalid input not rejected");
        Throws<ArgumentOutOfRangeException>(() => new BufferAutoSync(0));
    }),
    ("Repeated Auto Sync windows recompute a bounded delay without accumulating", () =>
    {
        var sync = new BufferAutoSync(); var results = new List<BufferSyncMeasurement>();
        for (var i = 0; i <= 280; i++)
        {
            var reading = sync.Observe(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 576, 960));
            if (reading.Measurement is { } result) results.Add(result);
        }
        Check(results.Count >= 4 && results.All(r => r.DelayA == 8 && r.DelayB == 0), "Repeated measurement accumulated delay");
    }),
    ("Auto Sync resets discard measurements and duplicate timestamps cannot fake a window", () =>
    {
        var sync = new BufferAutoSync();
        for (var i = 0; i < 100; i++) Check(sync.Observe(new(TimeSpan.Zero, i * 2400L, 480, 960)).Measurement is null, "Duplicate timestamp contributed to a window");
        sync.Reset();
        for (var i = 0; i < 80; i++) Check(sync.Observe(new(TimeSpan.FromMilliseconds(i * 50), i * 2400L, 480, 960)).Measurement is null, "Reset retained earlier measurements");
    }),
    ("Auto Sync preference persists with profiles and defaults off for existing settings", () =>
    {
        var store = new SettingsStore(Path.Combine(TestDirectory(), "settings.json"));
        store.Save(new() { Current = new() { AutoSyncEnabled = true, Speakers = [new() { DelayMs = 8 }, new()] }, Profiles = [new() { Name = "Automatic", AutoSyncEnabled = true }] });
        var loaded = store.Load();
        Check(loaded.Current.AutoSyncEnabled && loaded.Profiles[0].AutoSyncEnabled && loaded.Current.Speakers[0].DelayMs == 8, "Automatic mode or applied delay did not persist");
        var legacy = JsonSerializer.Deserialize<AppSettings>("""{"SchemaVersion":3,"Current":{"Speakers":[{"DelayMs":23},{"DelayMs":27}]}}""")!.Sanitize();
        Check(!legacy.Current.AutoSyncEnabled && legacy.Current.Speakers[0].DelayMs == 23 && legacy.Current.Speakers[1].DelayMs == 27, "Existing manual settings changed");
    }),
    ("Peaking filters are stable over all bands, rates, and gain limits", () =>
    {
        foreach (var rate in new[] { 44100, 48000, 96000 })
        foreach (var frequency in Presets.Frequencies)
        foreach (var db in new[] { -12.0, -6, 0, 6, 12 })
        {
            var c = BiquadCoefficients.Peak(rate, frequency, db);
            Check(c.IsStable, $"Unstable {rate}/{frequency}/{db}");
            Near(20 * Math.Log10(c.Magnitude(rate, frequency)), db, 0.00001);
            Near(c.Magnitude(rate, 0), 1, 0.000001);
        }
    }),
    ("Filter rejects invalid frequencies and nonfinite parameters", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => BiquadCoefficients.Peak(48000, 24000, 0));
        Throws<ArgumentOutOfRangeException>(() => BiquadCoefficients.Peak(48000, 1000, double.NaN));
        Throws<ArgumentOutOfRangeException>(() => BiquadCoefficients.Peak(48000, 0, 0));
    }),
    ("Measured sine response matches EQ without reducing unaffected audio", () =>
    {
        foreach (var gain in new[] { -12.0, 6, 12 })
        foreach (var frequency in new[] { 250.0, 1000, 4000 })
        {
            var bands = new double[10]; bands[5] = gain;
            var processor = new SpeakerProcessor(48000, new() { Volume = 1, Bands = bands });
            var signal = Tone(frequency, 2, 0.05); processor.Process(signal, 1);
            var expected = BiquadCoefficients.Peak(48000, 1000, gain).Magnitude(48000, frequency);
            Near(Rms(signal.AsSpan(96000)) / (0.05 / Math.Sqrt(2)), expected, 0.003);
            Check(processor.LimitedSamples == 0, "Normal test signal clipped");
        }
    }),
    ("Flat EQ and bypass preserve gain after the startup ramp", () =>
    {
        foreach (var enabled in new[] { true, false })
        {
            var processor = new SpeakerProcessor(48000, new() { Volume = 0.8, EqEnabled = enabled });
            var signal = Tone(1000, 1, 0.1); processor.Process(signal, 0.5);
            Near(Rms(signal.AsSpan(48000)), 0.1 * 0.4 / Math.Sqrt(2), 0.00001);
        }
    }),
    ("All routing modes produce the expected stereo matrix", () =>
    {
        foreach (var mode in Enum.GetValues<ChannelMode>())
        {
            var settings = new SpeakerSettings { Volume = 1, EqEnabled = false, ChannelMode = mode, LeftMix = 0.8, RightMix = 0.4 };
            var processor = new SpeakerProcessor(48000, settings);
            var signal = new float[48000];
            for (var i = 0; i < signal.Length; i += 2) { signal[i] = 0.2f; signal[i + 1] = -0.1f; }
            processor.Process(signal, 1); var m = SpeakerProcessor.Matrix(settings);
            Near(signal[^2], 0.2 * m.LL - 0.1 * m.LR, 0.00001);
            Near(signal[^1], 0.2 * m.RL - 0.1 * m.RR, 0.00001);
        }
    }),
    ("Integer and fractional delay place impulses at the right frames", () =>
    {
        foreach (var delay in new[] { 0.0, 1, 23, 23.25, 500 })
        {
            var processor = new SpeakerProcessor(48000, new() { Volume = 1, EqEnabled = false, DelayMs = delay });
            processor.Process(new float[48000], 1);
            var signal = new float[60000]; signal[0] = signal[1] = 0.5f;
            processor.Process(signal, 1);
            var frame = (int)(delay * 48) + processor.LimiterLatencyFrames; var frac = delay * 48 - (int)(delay * 48);
            Near(signal[frame * 2], 0.5 * (1 - frac), 0.00001);
            if (frac != 0) Near(signal[(frame + 1) * 2], 0.5 * frac, 0.00001);
            Near(signal.Sum(x => (double)x), 1, 0.0001);
        }
    }),
    ("Gain, mute, EQ, routing, and delay changes remain finite and bounded", () =>
    {
        var random = new Random(42); var processor = new SpeakerProcessor(48000, new());
        for (var block = 0; block < 300; block++)
        {
            processor.Update(new() { Volume = random.NextDouble(), Muted = block % 7 == 0, DelayMs = random.NextDouble() * 500,
                EqEnabled = block % 5 != 0, Bands = Enumerable.Range(0, 10).Select(_ => random.NextDouble() * 24 - 12).ToArray(), ChannelMode = (ChannelMode)(block % 5) });
            var signal = Tone(1000, 0.01, 0.7); processor.Process(signal, 1);
            Check(signal.All(x => float.IsFinite(x) && Math.Abs(x) <= 1), "Invalid or unbounded sample");
        }
    }),
    ("Mute and master-volume changes use a ramp", () =>
    {
        var processor = new SpeakerProcessor(48000, new() { Volume = 1, EqEnabled = false });
        var warmup = Enumerable.Repeat(0.5f, 48000).ToArray(); processor.Process(warmup, 1);
        var change = Enumerable.Repeat(0.5f, 9600).ToArray(); processor.Process(change, 0);
        Check(change[0] > 0.49 && change[^1] < 0.001, "Gain did not ramp to silence");
        Check(change.Zip(change.Skip(2), (a, b) => Math.Abs(a - b)).Max() < 0.002, "Abrupt gain step");
    }),
    ("Extreme boosts and invalid input cannot escape the safety clamp", () =>
    {
        var processor = new SpeakerProcessor(48000, new() { Volume = 1, Bands = Enumerable.Repeat(12.0, 10).ToArray() });
        var data = new float[48000]; for (var i = 0; i < data.Length; i++) data[i] = i % 3 == 0 ? float.NaN : 4;
        processor.Process(data, 1); Check(data.All(x => float.IsFinite(x) && Math.Abs(x) <= 1), "Unsafe output");
    }),
    ("Buffer wraparound preserves complete stereo frames", () =>
    {
        var buffer = new AudioBuffer(4);
        Check(buffer.Write([1, -1, 2, -2, 3, -3]) == 3, "Write failed");
        var a = new float[4]; buffer.Read(a); Check(a.SequenceEqual(new float[] { 1, -1, 2, -2 }), "Wrong initial frames");
        buffer.Write([4, -4, 5, -5, 6, -6]); var b = new float[8]; buffer.Read(b);
        Check(b.SequenceEqual(new float[] { 3, -3, 4, -4, 5, -5, 6, -6 }), "Wrong wrapped frames");
    }),
    ("Buffer counts overflow and fills underflow with silence", () =>
    {
        var buffer = new AudioBuffer(2); buffer.Write([1, 1, 2, 2, 3, 3]);
        Check(buffer.DroppedFrames == 1, "Missing overflow count");
        var data = new float[6]; Check(buffer.Read(data) == 2, "Wrong read count");
        Check(data[^1] == 0 && data[^2] == 0 && buffer.MissingFrames == 1, "Underflow not silent");
        Throws<ArgumentException>(() => buffer.Write([1]));
    }),
    ("Buffer publication is safe with simultaneous producer and consumer", () =>
    {
        const int frames = 100000; var buffer = new AudioBuffer(512);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var producer = Task.Run(() =>
        {
            float[] frame = new float[2];
            for (var i = 0; i < frames; i++)
            {
                while (buffer.AvailableFrames >= 512) { cancellation.Token.ThrowIfCancellationRequested(); Thread.Yield(); }
                frame[0] = i; frame[1] = -i; Check(buffer.Write(frame) == 1, "Lost frame");
            }
        });
        var consumer = Task.Run(() =>
        {
            float[] frame = new float[2];
            for (var i = 0; i < frames; i++)
            {
                while (buffer.AvailableFrames == 0) { cancellation.Token.ThrowIfCancellationRequested(); Thread.Yield(); }
                Check(buffer.Read(frame) == 1 && frame[0] == i && frame[1] == -i, $"Corrupted stereo frame {i}");
            }
        });
        Task.WaitAll([producer, consumer], cancellation.Token); Check(buffer.AvailableFrames == 0, "Buffer not drained");
    }),
    ("Profile validation rejects feedback, duplicate, and missing devices", () =>
    {
        var p = new AudioProfile { SourceId = "source", Speakers = [new() { DeviceId = "a" }, new() { DeviceId = "b" }] };
        RouteValidation.Validate(p, ["source", "a", "b"]);
        Throws<InvalidOperationException>(() => RouteValidation.Validate(p, ["a", "b"]));
        Throws<InvalidOperationException>(() => RouteValidation.Validate(p with { SourceId = "a" }, ["source", "a", "b"]));
        Throws<InvalidOperationException>(() => RouteValidation.Validate(p with { Speakers = [new() { DeviceId = "a" }, new() { DeviceId = "a" }] }, ["source", "a", "b"]));
    }),
    ("Settings preserve separate speaker state and sanitize corrupt values", () =>
    {
        var settings = new AppSettings { Current = new() { MasterVolume = double.NaN, Speakers = [new() { Volume = -1, DelayMs = 700, Bands = [double.NaN, 200] }, new() { Volume = 0.23, DelayMs = 19, ChannelMode = ChannelMode.Right }] } };
        var clean = settings.Sanitize(); Near(clean.Current.MasterVolume, 0.7, 0); Near(clean.Current.Speakers[0].Volume, 0, 0);
        Check(clean.Current.Speakers[0].Bands.Length == 10 && clean.Current.Speakers[0].Bands[1] == 12, "Bad band validation");
        Near(clean.Current.Speakers[0].DelayMs, 500, 0); Near(clean.Current.Speakers[1].Volume, 0.23, 0);
        var json = JsonSerializer.Serialize(clean, SettingsStore.JsonOptions);
        var restored = JsonSerializer.Deserialize<AppSettings>(json, SettingsStore.JsonOptions)!;
        Check(restored.Current.Speakers[1].ChannelMode == ChannelMode.Right, "Routing not persisted");
    }),
    ("Settings save atomically and keep malformed originals for recovery", () =>
    {
        var directory = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "settings.json"); var store = new SettingsStore(path);
        store.Save(new() { Current = new() { Name = "Room", MasterVolume = 0.32 } });
        Check(store.Load().Current.Name == "Room" && !File.Exists(path + ".tmp"), "Save did not complete");
        File.WriteAllText(path, "{broken"); var fallback = store.Load();
        Check(store.LoadWarning is not null && fallback.Current.Name == "My speakers", "Corruption not handled");
        Check(Directory.GetFiles(directory, "*.recovery-*").Length == 1, "Original not preserved");
        File.WriteAllText(path, "{\"SchemaVersion\":99}"); store.Load(); Check(store.LoadWarning is not null, "Future schema not rejected");
    }),
    ("Master gain preserves the ratio between speaker volumes", () =>
    {
        foreach (var master in new[] { 0.2, 0.8 })
        {
            var a = new SpeakerProcessor(48000, new() { Volume = 0.25, EqEnabled = false });
            var b = new SpeakerProcessor(48000, new() { Volume = 0.75, EqEnabled = false });
            var x = Tone(1000, 1, 0.1); var y = (float[])x.Clone(); a.Process(x, master); b.Process(y, master);
            Near(Rms(y.AsSpan(48000)) / Rms(x.AsSpan(48000)), 3, 0.0001);
        }
    }),
    ("EQ bypass and boost reset remain protected and recover normal volume", () =>
    {
        foreach (var bypass in new[] { true, false })
        {
            var bands = new double[10]; bands[5] = 12;
            var settings = new SpeakerSettings { Volume = 1, Bands = bands };
            var processor = new SpeakerProcessor(48000, settings);
            processor.Process(Tone(1000, 1, 0.8), 1);
            processor.Update(bypass ? settings with { EqEnabled = false } : settings with { Bands = new double[10] });
            var transition = Tone(1000, 1, 0.8); processor.Process(transition, 1);
            Check(transition.Max(x => Math.Abs(x)) <= 0.981, "EQ transition exceeded the peak ceiling");
            Near(Rms(transition.AsSpan(72000)), 0.8 / Math.Sqrt(2), 0.002);
            Check(processor.LimitedSamples == 0, "EQ transition reached the safety clamp");
        }
    }),
    ("Boosting multiple EQ bands does not stack whole-signal attenuation", () =>
    {
        var bands = Enumerable.Repeat(6.0, 10).ToArray();
        var processor = new SpeakerProcessor(48000, new() { Volume = 1, Bands = bands });
        var signal = Tone(1000, 2, 0.01); processor.Process(signal, 1);
        var expected = Presets.Frequencies.Select(f => BiquadCoefficients.Peak(48000, f, 6).Magnitude(48000, 1000)).Aggregate(1.0, (a, b) => a * b);
        Near(Rms(signal.AsSpan(96000)) / (0.01 / Math.Sqrt(2)), expected, 0.01);
        Check(processor.LimiterReductionDb < 0.01, "Ordinary quiet audio was attenuated");
    }),
    ("Limiter is transparent below its ceiling and links stereo peaks", () =>
    {
        var limiter = new PeakLimiter(48000);
        for (var i = 0; i < 1000; i++)
        {
            var output = limiter.Process(0.3, -0.15);
            if (i >= limiter.LatencyFrames) { Near(output.Left, 0.3, 0.000001); Near(output.Right, -0.15, 0.000001); }
        }
        for (var i = 0; i < 1000; i++)
        {
            var output = limiter.Process(4, -2);
            Check(Math.Abs(output.Left) <= 0.981 && Math.Abs(output.Right) <= 0.981, "Limiter exceeded ceiling");
            if (Math.Abs(output.Left) > 0.001) Near(output.Right / output.Left, -0.5, 0.00001);
        }
    }),
    ("Windows mute and zero source volume silence every master mode", () =>
    {
        foreach (var mode in Enum.GetValues<MasterMode>())
        foreach (var included in new[] { true, false })
        {
            var profile = new AudioProfile { MasterMode = mode, MasterVolume = 0.7, LoopbackIncludesEndpointVolume = included };
            Near(MasterGainPolicy.Calculate(profile, 0.7, true), 0, 0);
            Near(MasterGainPolicy.Calculate(profile, 0, false), 0, 0);
            var expected = mode == MasterMode.AppOnly ? 0.7 : included ? 1 : 0.25;
            Near(MasterGainPolicy.Calculate(profile, 0.25, false), expected, 0);
        }
    }),
    ("Legacy default profiles migrate to Windows volume without losing speaker settings", () =>
    {
        var directory = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new AppSettings { SchemaVersion = 1, Current = new() { MasterMode = MasterMode.AppOnly, Speakers = [new() { Volume = 0.23, DelayMs = 42 }, new()] } }, SettingsStore.JsonOptions));
        var migrated = new SettingsStore(path).Load();
        Check(migrated.SchemaVersion == 3 && migrated.Current.MasterMode == MasterMode.SourceEndpoint && !migrated.Current.LoopbackIncludesEndpointVolume, "Windows volume mode not migrated");
        Near(migrated.Current.Speakers[0].Volume, 0.23, 0); Near(migrated.Current.Speakers[0].DelayMs, 42, 0);
    }),
    ("Single EQ captures are independent and leave every non-EQ speaker setting intact", () =>
    {
        var original = new SpeakerSettings { Bands = Presets.Get("Music"), EqEnabled = false };
        var library = new EqPresetLibrary(); library.SaveSingle(" Rock ", original);
        original.Bands[0] = -12;
        var target = new SpeakerSettings { DeviceId = "speaker-b", Volume = 0.23, Muted = true, DelayMs = 149.5, ChannelMode = ChannelMode.Custom, LeftMix = 0.3, RightMix = 0.9 };
        var loaded = library.LoadSingle("ROCK"); var result = loaded.ApplyTo(target);
        Check(result.Bands.SequenceEqual(Presets.Get("Music")) && !result.EqEnabled, "Saved EQ was changed or not restored");
        Check((result with { Bands = target.Bands, EqEnabled = target.EqEnabled }) == target, "Applying EQ changed a non-EQ setting");
        loaded.Bands[0] = 9; result.Bands[1] = -9;
        Check(library.LoadSingle("rock").Bands.SequenceEqual(Presets.Get("Music")), "Loading shares saved band arrays");
    }),
    ("Dual EQ preserves distinct A and B curves without sharing their bands", () =>
    {
        var a = new SpeakerSettings { Bands = Presets.Get("Music"), EqEnabled = true };
        var b = new SpeakerSettings { Bands = Presets.Get("Voice"), EqEnabled = false };
        var library = new EqPresetLibrary(); library.SaveDual("Indie", a, b);
        a.Bands[0] = b.Bands[0] = 12;
        var pair = library.LoadDual("indie");
        Check(pair.A.Bands.SequenceEqual(Presets.Get("Music")) && pair.B.Bands.SequenceEqual(Presets.Get("Voice")), "Sides were swapped or changed");
        Check(pair.A.Enabled && !pair.B.Enabled, "Per-speaker enable flags were lost");
        pair.A.Bands[0] = pair.B.Bands[0] = 0;
        Check(library.LoadDual("Indie").B.Bands[0] == -6, "Dual load mutated the saved pair");
        var snapshot = library.Duals; snapshot[0].A.Bands[0] = -12;
        Check(library.LoadDual("Indie").A.Bands[0] == 2, "Snapshot exposes saved bands");
    }),
    ("EQ libraries replace names without duplicates and delete independently", () =>
    {
        var library = new EqPresetLibrary();
        library.SaveSingle("Rock", new()); library.SaveSingle(" ROCK ", new() { Bands = Presets.Get("Music") });
        library.SaveDual("Rock", new(), new());
        Check(library.Singles.Length == 1 && library.Duals.Length == 1, "Names were not unique within each library");
        Check(library.LoadSingle("rock").Bands[0] == 2, "Replacement failed");
        library.DeleteSingle("rOcK"); Check(library.Singles.Length == 0 && library.Duals.Length == 1, "Delete affected the other library");
        library.DeleteDual("ROCK"); Check(library.Duals.Length == 0, "Dual deletion failed");
        Throws<ArgumentException>(() => library.SaveSingle("   ", new()));
        Throws<ArgumentException>(() => library.SaveDual(new string('x', 81), new(), new()));
        for (var i = 0; i < EqPresetLibrary.MaximumPresets; i++) library.SaveSingle($"EQ {i}", new());
        Throws<InvalidOperationException>(() => library.SaveSingle("Overflow", new()));
        library.SaveSingle("EQ 0", new() { EqEnabled = false });
        Check(!library.LoadSingle("EQ 0").Enabled, "Full library prevented replacement");
    }),
    ("EQ libraries survive disk reload alongside full setup profiles", () =>
    {
        var library = new EqPresetLibrary();
        library.SaveSingle("Rock", new() { Bands = Presets.Get("Music") });
        library.SaveSingle("Pop", new() { Bands = Presets.Get("Movies") });
        library.SaveDual("Indie", new() { Bands = Presets.Get("Music") }, new() { Bands = Presets.Get("Voice"), EqEnabled = false });
        var store = new SettingsStore(Path.Combine(TestDirectory(), "settings.json"));
        store.Save(new() { EqPresets = library.Singles.ToList(), DualEqPresets = library.Duals.ToList(), Profiles = [new() { Name = "Room", SourceId = "cable" }] });
        var settings = store.Load(); var restored = new EqPresetLibrary(settings.EqPresets, settings.DualEqPresets);
        Check(store.LoadWarning is null && settings.Profiles[0].SourceId == "cable", "Existing profile lost on save");
        Check(restored.Singles.Length == 2 && restored.LoadSingle("Pop").Bands.SequenceEqual(Presets.Get("Movies")), "Single EQ did not persist");
        var pair = restored.LoadDual("Indie");
        Check(pair.A.Bands.SequenceEqual(Presets.Get("Music")) && pair.B.Bands.SequenceEqual(Presets.Get("Voice")) && !pair.B.Enabled, "Dual EQ did not persist");
    }),
    ("Schema 2 settings migrate without losing EQ or profiles", () =>
    {
        var path = Path.Combine(TestDirectory(), "settings.json");
        File.WriteAllText(path, """{"SchemaVersion":2,"Current":{"Name":"Room","Speakers":[{"Bands":[3,2,1],"DelayMs":27},{"EqEnabled":false,"Volume":0.23}]},"Profiles":[{"Name":"Old setup"}]}""");
        var store = new SettingsStore(path); var settings = store.Load();
        Check(store.LoadWarning is null && settings.SchemaVersion == 3 && settings.EqPresets.Count == 0 && settings.DualEqPresets.Count == 0, "Migration failed");
        Near(settings.Current.Speakers[0].Bands[0], 3, 0); Near(settings.Current.Speakers[0].DelayMs, 27, 0);
        Check(!settings.Current.Speakers[1].EqEnabled && settings.Profiles[0].Name == "Old setup", "Migration changed existing settings");
    }),
    ("Malformed saved EQ entries are sanitized without discarding other settings", () =>
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"EqPresets":[null,{"Name":" "},{"Name":"Rock","Eq":{"Bands":[90,-90]}},{"Name":"rock"}],"DualEqPresets":[{"Name":"Pair","A":null,"B":{"Enabled":false,"Bands":null}}]}""")!.Sanitize();
        Check(settings.EqPresets.Count == 1 && settings.EqPresets[0].Eq.Bands.Length == 10, "Invalid or duplicate entries survived");
        Near(settings.EqPresets[0].Eq.Bands[0], 12, 0); Near(settings.EqPresets[0].Eq.Bands[1], -12, 0);
        Check(settings.DualEqPresets[0].A.Enabled && !settings.DualEqPresets[0].B.Enabled && settings.DualEqPresets[0].B.Bands.All(b => b == 0), "Missing EQ data was not recovered");
    }),
    ("Cable detection accepts standard endpoints and excludes other VB-Audio products", () =>
    {
        Check(CablePackage.IsCableName("CABLE Input (VB-Audio Virtual Cable)"), "Cable input not recognized");
        Check(CablePackage.IsCableName("Speakers (2- VB-Audio Virtual Cable)"), "Older cable endpoint not recognized");
        Check(!CablePackage.IsCableName("Voicemeeter Input (VB-Audio Voicemeeter VAIO)"), "Voicemeeter mistaken for the standard cable");
        Check(!CablePackage.IsCableName("CABLE-A Input (VB-Audio Cable A)"), "Paid extra cable mistaken for the standard package");
    }),
    ("Cable package integrity check rejects modified downloads before extraction", () =>
    {
        var directory = TestDirectory(); var archive = Path.Combine(directory, "cable.zip");
        WriteTestZip(archive, false); var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
        File.AppendAllText(archive, "modified"); var destination = Path.Combine(directory, "package");
        Throws<InvalidDataException>(() => CablePackage.ExtractVerifiedAsync(archive, destination, hash).GetAwaiter().GetResult());
        Check(!Directory.Exists(destination), "Invalid package wrote extracted files");
    }),
    ("Verified cable archive preserves the installer and license files", () =>
    {
        var directory = TestDirectory(); var archive = Path.Combine(directory, "cable.zip");
        WriteTestZip(archive, false); var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
        var destination = Path.Combine(directory, "package");
        CablePackage.ExtractVerifiedAsync(archive, destination, hash).GetAwaiter().GetResult();
        Check(File.ReadAllText(Path.Combine(destination, "readme.txt")) == "test license", "License contents changed");
        Check(File.Exists(Path.Combine(destination, CablePackage.InstallerName)), "Installer missing");
        Throws<IOException>(() => CablePackage.ExtractVerifiedAsync(archive, destination, hash).GetAwaiter().GetResult());
    }),
    ("Cable extraction rejects paths escaping its owned directory", () =>
    {
        var directory = TestDirectory(); var archive = Path.Combine(directory, "cable.zip");
        WriteTestZip(archive, true); var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
        var destination = Path.Combine(directory, "package");
        Throws<InvalidDataException>(() => CablePackage.ExtractVerifiedAsync(archive, destination, hash).GetAwaiter().GetResult());
        Check(!File.Exists(Path.Combine(directory, "outside.txt")) && !Directory.Exists(destination), "Unsafe archive wrote files");
    }),
    ("Cancelled cable preparation does not extract a package", () =>
    {
        var directory = TestDirectory(); var archive = Path.Combine(directory, "cable.zip");
        WriteTestZip(archive, false); var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var destination = Path.Combine(directory, "package");
        Throws<OperationCanceledException>(() => CablePackage.ExtractVerifiedAsync(archive, destination, hash, cancellation.Token).GetAwaiter().GetResult());
        Check(!Directory.Exists(destination), "Cancelled extraction wrote files");
    }),
    ("Processing avoids managed allocations after warmup", () =>
    {
        var processor = new SpeakerProcessor(48000, new()); var signal = new float[960];
        for (var i = 0; i < 20; i++) processor.Process(signal, 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) processor.Process(signal, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated < 1024, $"Processing allocated {allocated} bytes");
    })
};
var failures = 0; var time = Stopwatch.StartNew();
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + test.Name + "\n" + ex); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed in {time.Elapsed.TotalSeconds:0.00}s.");
return failures == 0 ? 0 : 1;

static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
static void Near(double actual, double expected, double tolerance) => Check(Math.Abs(actual - expected) <= tolerance, $"Expected {expected:G9}, got {actual:G9} (±{tolerance})");
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
static float[] Tone(double frequency, double seconds, double amplitude)
{
    var result = new float[(int)(48000 * seconds) * 2];
    for (var i = 0; i < result.Length; i += 2) result[i] = result[i + 1] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * (i / 2) / 48000));
    return result;
}
static double Rms(ReadOnlySpan<float> signal) { double sum = 0; foreach (var sample in signal) sum += sample * sample; return Math.Sqrt(sum / signal.Length); }
static string TestDirectory()
{
    var path = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path); return path;
}
static void WriteTestZip(string path, bool unsafePath)
{
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
    using (var writer = new StreamWriter(archive.CreateEntry(CablePackage.InstallerName).Open())) writer.Write("inert test fixture, not an executable");
    using (var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open())) writer.Write("test license");
    if (unsafePath) { using var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open()); writer.Write("must not extract"); }
}
