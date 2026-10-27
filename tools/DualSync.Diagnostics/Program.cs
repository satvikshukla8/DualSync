using System.Text.Json;
using DualSync.Audio;
using DualSync.Core;
using System.Diagnostics;

try
{
    if (args.Length == 1 && args[0] == "--benchmark")
    {
        var bands = Presets.Get("Music");
        var a = new SpeakerProcessor(48000, new() { Bands = bands });
        var b = new SpeakerProcessor(48000, new() { Bands = bands, DelayMs = 100 });
        var block = new float[960];
        for (var i = 0; i < 20; i++) { a.Process(block, 0.7); b.Process(block, 0.7); }
        var timer = Stopwatch.StartNew(); var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
        for (var chunk = 0; chunk < 6000; chunk++)
        {
            for (var i = 0; i < block.Length; i += 2) block[i] = block[i + 1] = (float)(0.1 * Math.Sin(i * Math.PI / 48));
            a.Process(block, 0.7); b.Process(block, 0.7);
        }
        timer.Stop(); process.Refresh();
        Console.WriteLine(JsonSerializer.Serialize(new { simulatedAudioSeconds = 60, speakers = 2, wallSeconds = timer.Elapsed.TotalSeconds,
            cpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, note = "Offline DSP benchmark; excludes WASAPI, UI, and Bluetooth transport." }, SettingsStore.JsonOptions));
        return 0;
    }
    var devices = DeviceCatalog.Enumerate();
    if (args.Length == 0 || args[0] == "--list")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { operatingSystem = Environment.OSVersion.ToString(), devices }, SettingsStore.JsonOptions));
        return 0;
    }
    if (args.Length != 4 || (args[0] != "--route" && args[0] != "--smoke"))
    {
        Console.Error.WriteLine("Usage: DualSync.Diagnostics --list\n       DualSync.Diagnostics --benchmark\n       DualSync.Diagnostics --route SOURCE_ID OUTPUT_A_ID OUTPUT_B_ID\n       DualSync.Diagnostics --smoke SOURCE_ID OUTPUT_A_ID OUTPUT_B_ID\n--route plays captured system audio for 10 seconds. --smoke tests two 3-second start/stop cycles with zero output gain. Neither changes default devices or endpoint volumes.");
        return 2;
    }
    await using var engine = new AudioEngine();
    string? failure = null; engine.Faulted += error => { failure = error; Console.Error.WriteLine(error); };
    var silent = args[0] == "--smoke";
    for (var cycle = 0; cycle < (silent ? 2 : 1); cycle++)
    {
        await engine.StartAsync(new AudioProfile { SourceId = args[1], MasterMode = MasterMode.AppOnly, MasterVolume = silent ? 0 : 0.5, Speakers = [new() { DeviceId = args[2] }, new() { DeviceId = args[3] }] });
        for (var i = 0; i < (silent ? 3 : 10); i++)
        {
            await Task.Delay(1000);
            var outputs = engine.Statistics();
            Console.WriteLine(JsonSerializer.Serialize(new { cycle = cycle + 1, engine.IsRunning, engine.CapturedFrames, outputs }));
            if (failure is not null) return 1;
            if (silent && outputs.Any(output => output.Peak != 0)) throw new InvalidOperationException("Silent smoke test produced nonzero output.");
        }
        await engine.StopAsync();
        if (engine.IsRunning || engine.Statistics().Length != 0) throw new InvalidOperationException("Engine did not release its streams.");
    }
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
