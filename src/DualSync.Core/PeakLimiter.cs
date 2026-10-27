namespace DualSync.Core;

// Stereo-linked peak protection. Normal-level audio passes at unity gain.
// Five milliseconds of lookahead allow a short linear attack before a loud peak.
public sealed class PeakLimiter
{
    public int LatencyFrames { get; }
    public double GainReductionDb => -20 * Math.Log10(Math.Max(1e-30, gain));
    public long ProtectedFrames { get; private set; }
    private readonly double[] audio, peaks;
    private readonly long[] indices;
    private readonly double release;
    private int position, head, count;
    private long frame;
    private double gain = 1;
    private const double Ceiling = 0.98;

    public PeakLimiter(int rate)
    {
        if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
        LatencyFrames = Math.Max(1, (int)Math.Round(rate * 0.005));
        audio = new double[LatencyFrames * 2];
        peaks = new double[LatencyFrames + 2]; indices = new long[peaks.Length];
        release = 1 - Math.Exp(-1.0 / (rate * 0.100));
    }
    public (float Left, float Right) Process(double left, double right)
    {
        left = double.IsFinite(left) ? left : 0; right = double.IsFinite(right) ? right : 0;
        var oldest = frame - LatencyFrames;
        while (count > 0 && indices[head] < oldest) { head = (head + 1) % peaks.Length; count--; }
        var peak = Math.Max(Math.Abs(left), Math.Abs(right));
        while (count > 0 && peaks[(head + count - 1) % peaks.Length] <= peak) count--;
        var tail = (head + count) % peaks.Length;
        peaks[tail] = peak; indices[tail] = frame; count++;
        var required = peaks[head] > Ceiling ? Ceiling / peaks[head] : 1;
        gain = required < gain ? Math.Max(required, gain - 1.0 / LatencyFrames) : Math.Min(required, gain + (1 - gain) * release);
        var delayedLeft = audio[position * 2]; var delayedRight = audio[position * 2 + 1];
        audio[position * 2] = left; audio[position * 2 + 1] = right;
        position = (position + 1) % LatencyFrames; frame++;
        // A numerical guard also covers arbitrarily large input steps during an attack.
        var delayedPeak = Math.Max(Math.Abs(delayedLeft), Math.Abs(delayedRight));
        var applied = delayedPeak > Ceiling ? Math.Min(gain, Ceiling / delayedPeak) : gain;
        if (applied < 0.99999) ProtectedFrames++;
        return ((float)(delayedLeft * applied), (float)(delayedRight * applied));
    }
}
