using System.Runtime.InteropServices;
using DualSync.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DualSync.Audio;

public sealed record OutputStatistics(int Speaker, int BufferedFrames, long UnderrunFrames, long OverrunFrames, long LimitedSamples, float Peak, double HeadroomDb);

public sealed class AudioEngine : IAsyncDisposable
{
    public const int SampleRate = 48000;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private WasapiRecorder? recorder;
    private MMDevice? source;
    private Output[] outputs = [];
    private AudioProfile profile = new();
    private double sourceVolume = 1;
    private int sourceMuted, running, stopping;
    private long capturedFrames;
    public bool IsRunning => Volatile.Read(ref running) == 1;
    public double SourceVolume => Volatile.Read(ref sourceVolume);
    public long CapturedFrames => Interlocked.Read(ref capturedFrames);
    public event Action<string>? Faulted;
    public event Action<double>? SourceVolumeChanged;

    public async Task StartAsync(AudioProfile configuration)
    {
        await lifecycle.WaitAsync();
        try
        {
            await StopInternalAsync();
            var clean = configuration.Sanitize();
            RouteValidation.Validate(clean, DeviceCatalog.Enumerate().Select(d => d.Id));
            Volatile.Write(ref profile, clean);
            using var enumerator = new MMDeviceEnumerator();
            source = enumerator.GetDevice(clean.SourceId!);
            sourceVolume = source.AudioEndpointVolume.MasterVolumeLevelScalar;
            sourceMuted = source.AudioEndpointVolume.Mute ? 1 : 0;
            source.AudioEndpointVolume.OnVolumeNotification += OnSourceVolume;
            var created = new List<Output>();
            try
            {
                for (var i = 0; i < clean.Speakers.Length; i++)
                {
                    var device = enumerator.GetDevice(clean.Speakers[i].DeviceId!);
                    try { created.Add(new Output(this, device, clean.Speakers[i])); }
                    catch { device.Dispose(); throw; }
                }
                Volatile.Write(ref outputs, created.ToArray());
                recorder = new WasapiRecorderBuilder().WithDevice(source).WithLoopbackCapture()
                    .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2))
                    .WithBufferLength(40).WithMmcssThreadPriority("Audio").Build();
                recorder.DataAvailable += OnData;
                recorder.RecordingStopped += (_, e) => { if (e.Exception is not null) ReportFault(e.Exception); };
                Interlocked.Exchange(ref capturedFrames, 0);
                Volatile.Write(ref running, 1);
                recorder.StartRecording();
                foreach (var output in outputs) output.Player.Play();
            }
            catch
            {
                // Items already published in outputs are released by StopInternalAsync.
                if (outputs.Length == 0) foreach (var output in created) await output.DisposeAsync();
                await StopInternalAsync();
                throw;
            }
        }
        finally { lifecycle.Release(); }
    }

    private void OnData(ReadOnlySpan<byte> bytes, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        var data = MemoryMarshal.Cast<byte, float>(bytes);
        var complete = data[..(data.Length / 2 * 2)];
        foreach (var output in Volatile.Read(ref outputs)) output.Buffer.Write(complete);
        Interlocked.Add(ref capturedFrames, complete.Length / 2);
    }
    private void OnSourceVolume(AudioVolumeNotificationData data)
    {
        Volatile.Write(ref sourceVolume, data.MasterVolume);
        Volatile.Write(ref sourceMuted, data.Muted ? 1 : 0);
        SourceVolumeChanged?.Invoke(data.MasterVolume);
    }
    private double MasterGain
    {
        get
        {
            var current = Volatile.Read(ref profile);
            if (current.MasterMode == MasterMode.AppOnly) return current.MasterVolume;
            if (Volatile.Read(ref sourceMuted) != 0) return 0;
            return current.LoopbackIncludesEndpointVolume ? 1 : SourceVolume;
        }
    }
    public void Update(AudioProfile configuration)
    {
        var clean = configuration.Sanitize();
        Volatile.Write(ref profile, clean);
        var snapshot = Volatile.Read(ref outputs);
        for (var i = 0; i < snapshot.Length && i < clean.Speakers.Length; i++) snapshot[i].Processor.Update(clean.Speakers[i]);
    }
    // Call only for a deliberate master-slider gesture after the user opts into endpoint control.
    public void SetSourceVolume(double value)
    {
        if (source is null || !IsRunning) throw new InvalidOperationException("Start audio before controlling the source volume.");
        source.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(value, 0, 1);
    }
    public OutputStatistics[] Statistics() => Volatile.Read(ref outputs).Select((o, i) =>
        new OutputStatistics(i, o.Buffer.AvailableFrames, o.Buffer.MissingFrames, o.Buffer.DroppedFrames,
            o.Processor.LimitedSamples, o.Processor.Peak, o.Processor.HeadroomDb)).ToArray();
    private void ReportFault(Exception exception)
    {
        if (Volatile.Read(ref stopping) == 0 && Interlocked.Exchange(ref running, 0) != 0)
            Faulted?.Invoke("Audio device stopped: " + exception.Message);
    }
    public async Task StopAsync()
    {
        await lifecycle.WaitAsync();
        try { await StopInternalAsync(); }
        finally { lifecycle.Release(); }
    }
    private async Task StopInternalAsync()
    {
        Volatile.Write(ref stopping, 1); Volatile.Write(ref running, 0);
        var capture = recorder; recorder = null;
        if (capture is not null)
        {
            capture.DataAvailable -= OnData;
            await capture.DisposeAsync();
        }
        var oldOutputs = Interlocked.Exchange(ref outputs, []);
        foreach (var output in oldOutputs) await output.DisposeAsync();
        if (source is not null)
        {
            source.AudioEndpointVolume.OnVolumeNotification -= OnSourceVolume;
            source.Dispose(); source = null;
        }
        Volatile.Write(ref stopping, 0);
    }
    public async ValueTask DisposeAsync() { await StopAsync(); lifecycle.Dispose(); }

    private sealed class Output : ISampleProvider, IAsyncDisposable
    {
        private readonly AudioEngine engine;
        private readonly MMDevice device;
        public DualSync.Core.AudioBuffer Buffer { get; } = new(SampleRate / 2);
        public SpeakerProcessor Processor { get; }
        public WasapiPlayer Player { get; }
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);
        public Output(AudioEngine owner, MMDevice endpoint, SpeakerSettings settings)
        {
            engine = owner; device = endpoint;
            Processor = new(SampleRate, settings);
            Player = new WasapiPlayerBuilder().WithDevice(endpoint).WithSharedMode().WithLatency(80)
                .WithMmcssThreadPriority("Audio").Build();
            try { Player.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider(this)); }
            catch { Player.Dispose(); throw; }
            Player.PlaybackStopped += (_, e) => { if (e.Exception is not null) engine.ReportFault(e.Exception); };
        }
        public int Read(Span<float> samples)
        {
            var complete = samples[..(samples.Length / 2 * 2)];
            Buffer.Read(complete);
            Processor.Process(complete, engine.MasterGain);
            samples[complete.Length..].Clear();
            return samples.Length;
        }
        public async ValueTask DisposeAsync() { try { await Player.DisposeAsync(); } finally { device.Dispose(); } }
    }
}
