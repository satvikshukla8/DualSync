namespace DualSync.Core;

public enum BufferSyncState { Settling, WaitingForAudio, Unstable, Measuring, Aligned }

public readonly record struct BufferSyncSample(TimeSpan Time, long CapturedFrames, int QueueA, int QueueB,
    long MissingA = 0, long MissingB = 0, long DroppedA = 0, long DroppedB = 0);
public sealed record BufferSyncMeasurement(double AverageA, double AverageB, double DelayA, double DelayB, int Samples);
public readonly record struct BufferSyncReading(BufferSyncState State, double Progress = 0, BufferSyncMeasurement? Measurement = null);

// Samples the two application queues together, outside the real-time audio callbacks.
// This cannot observe output-driver or Bluetooth/device latency.
public sealed class BufferAutoSync(int sampleRate = 48000)
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumGap = TimeSpan.FromMilliseconds(250);
    public const int MinimumSamples = 40;
    private readonly int rate = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));
    private BufferSyncSample? previous;
    private TimeSpan settleUntil, windowStart;
    private int count;
    private double sumA, sumB;

    public void Reset() { previous = null; ResetWindow(); }
    private void ResetWindow() { count = 0; sumA = sumB = 0; }
    private BufferSyncReading Restart(BufferSyncSample sample, BufferSyncState state)
    {
        previous = sample; settleUntil = sample.Time + Warmup; ResetWindow();
        return new(state);
    }

    public BufferSyncReading Observe(BufferSyncSample sample)
    {
        if (sample.Time < TimeSpan.Zero || sample.CapturedFrames < 0 || sample.QueueA < 0 || sample.QueueB < 0
            || sample.QueueA > rate / 2 || sample.QueueB > rate / 2
            || sample.MissingA < 0 || sample.MissingB < 0 || sample.DroppedA < 0 || sample.DroppedB < 0)
        { Reset(); return new(BufferSyncState.Unstable); }
        if (previous is not { } last) return Restart(sample, BufferSyncState.Settling);
        if (sample.Time <= last.Time || sample.Time - last.Time > MaximumGap || sample.CapturedFrames < last.CapturedFrames)
            return Restart(sample, BufferSyncState.Settling);
        previous = sample;
        if (sample.CapturedFrames == last.CapturedFrames) return Restart(sample, BufferSyncState.WaitingForAudio);
        if (sample.MissingA != last.MissingA || sample.MissingB != last.MissingB
            || sample.DroppedA != last.DroppedA || sample.DroppedB != last.DroppedB)
            return Restart(sample, BufferSyncState.Unstable);
        if (sample.Time < settleUntil) return new(BufferSyncState.Settling);

        if (count == 0) windowStart = sample.Time;
        count++; sumA += sample.QueueA; sumB += sample.QueueB;
        var elapsed = sample.Time - windowStart;
        if (elapsed < Window) return new(BufferSyncState.Measuring, Math.Clamp(elapsed.TotalSeconds / Window.TotalSeconds * 100, 0, 100));
        if (count < MinimumSamples) { ResetWindow(); return new(BufferSyncState.Unstable); }
        var a = sumA / count * 1000 / rate; var b = sumB / count * 1000 / rate;
        var target = Math.Max(a, b);
        // Recompute both delays from zero. Including previous added delay would ratchet the result.
        var result = new BufferSyncMeasurement(a, b, Math.Round(target - a, 1), Math.Round(target - b, 1), count);
        ResetWindow();
        return new(BufferSyncState.Aligned, 100, result);
    }
}
