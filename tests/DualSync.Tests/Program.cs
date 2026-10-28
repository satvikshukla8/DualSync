using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DualSync.Core;

var tests = new (string Name, Action Run)[]
{
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
        Check(migrated.SchemaVersion == 2 && migrated.Current.MasterMode == MasterMode.SourceEndpoint && !migrated.Current.LoopbackIncludesEndpointVolume, "Windows volume mode not migrated");
        Near(migrated.Current.Speakers[0].Volume, 0.23, 0); Near(migrated.Current.Speakers[0].DelayMs, 42, 0);
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
