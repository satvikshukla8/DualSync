namespace DualSync.Core;

// One producer and one consumer. Publication happens after the frame copy.
// Overflow drops incoming frames; neither side writes the other side's cursor.
public sealed class AudioBuffer
{
    private readonly float[] data;
    private readonly int capacity;
    private long read, write, dropped, missing;
    public AudioBuffer(int capacityFrames)
    {
        if (capacityFrames < 2) throw new ArgumentOutOfRangeException(nameof(capacityFrames));
        capacity = capacityFrames; data = new float[capacity * 2];
    }
    public int AvailableFrames => (int)Math.Clamp(Volatile.Read(ref write) - Volatile.Read(ref read), 0, capacity);
    public long DroppedFrames => Interlocked.Read(ref dropped);
    public long MissingFrames => Interlocked.Read(ref missing);
    public int Write(ReadOnlySpan<float> stereo)
    {
        if (stereo.Length % 2 != 0) throw new ArgumentException("Stereo frames must be complete.");
        var w = write;
        var frames = Math.Min(stereo.Length / 2, capacity - (int)(w - Volatile.Read(ref read)));
        for (var i = 0; i < frames; i++)
        {
            var p = (int)((w + i) % capacity) * 2;
            data[p] = stereo[2 * i]; data[p + 1] = stereo[2 * i + 1];
        }
        Volatile.Write(ref write, w + frames);
        Interlocked.Add(ref dropped, stereo.Length / 2 - frames);
        return frames;
    }
    public int Read(Span<float> stereo)
    {
        if (stereo.Length % 2 != 0) throw new ArgumentException("Stereo frames must be complete.");
        var r = read;
        var frames = Math.Min(stereo.Length / 2, (int)(Volatile.Read(ref write) - r));
        for (var i = 0; i < frames; i++)
        {
            var p = (int)((r + i) % capacity) * 2;
            stereo[2 * i] = data[p]; stereo[2 * i + 1] = data[p + 1];
        }
        stereo[(frames * 2)..].Clear();
        Volatile.Write(ref read, r + frames);
        Interlocked.Add(ref missing, stereo.Length / 2 - frames);
        return frames;
    }
}
