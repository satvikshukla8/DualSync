namespace DualSync.Core;

public sealed class SpeakerProcessor
{
    private readonly int rate;
    private SpeakerSettings target;
    private readonly BiquadState[] left = new BiquadState[10], right = new BiquadState[10];
    private readonly BiquadCoefficients[] coefficients = new BiquadCoefficients[10];
    private readonly double[] bandGains = new double[10];
    private readonly float[] delay;
    private int position, coefficientClock;
    private double gain, headroom = 1, eqWet = 1, currentDelay, oldDelay, delayFade = 1;
    private double ll = 1, lr, rl, rr = 1;
    private long limited;
    private float peak;
    public long LimitedSamples => Interlocked.Read(ref limited);
    public float Peak => Volatile.Read(ref peak);
    public double HeadroomDb => -20 * Math.Log10(headroom);

    public SpeakerProcessor(int sampleRate, SpeakerSettings settings)
    {
        rate = sampleRate; target = settings.Sanitize();
        delay = new float[((int)(sampleRate * 0.5) + 4) * 2];
        currentDelay = oldDelay = target.DelayMs * rate / 1000;
        for (var i = 0; i < 10; i++) coefficients[i] = BiquadCoefficients.Peak(rate, Math.Min(Presets.Frequencies[i], rate * 0.45), 0);
    }
    public void Update(SpeakerSettings settings) => Volatile.Write(ref target, settings.Sanitize());
    public void Process(Span<float> stereo, double masterGain)
    {
        var s = Volatile.Read(ref target);
        var targetGain = s.Muted ? 0 : s.Volume * Math.Clamp(masterGain, 0, 1);
        var targetHeadroom = s.EqEnabled ? Numbers.DbToGain(-s.Bands.Sum(v => Math.Max(0, v))) : 1;
        var wantedDelay = s.DelayMs * rate / 1000;
        if (delayFade >= 1 && Math.Abs(wantedDelay - currentDelay) > 0.01)
        { oldDelay = currentDelay; currentDelay = wantedDelay; delayFade = 0; }
        var (tll, tlr, trl, trr) = Matrix(s);
        var smooth = 1 - Math.Exp(-1.0 / (rate * 0.015));
        var blockPeak = 0f;
        for (var p = 0; p + 1 < stereo.Length; p += 2)
        {
            gain += (targetGain - gain) * smooth;
            headroom += (targetHeadroom - headroom) * smooth;
            eqWet += ((s.EqEnabled ? 1 : 0) - eqWet) * smooth;
            ll += (tll - ll) * smooth; lr += (tlr - lr) * smooth;
            rl += (trl - rl) * smooth; rr += (trr - rr) * smooth;
            if (coefficientClock++ % 32 == 0)
            {
                var bandSmooth = 1 - Math.Exp(-32.0 / (rate * 0.03));
                for (var b = 0; b < 10; b++)
                {
                    bandGains[b] += (s.Bands[b] - bandGains[b]) * bandSmooth;
                    coefficients[b] = BiquadCoefficients.Peak(rate, Math.Min(Presets.Frequencies[b], rate * 0.45), bandGains[b]);
                }
            }
            var inputL = float.IsFinite(stereo[p]) ? stereo[p] : 0;
            var inputR = float.IsFinite(stereo[p + 1]) ? stereo[p + 1] : 0;
            var dryL = inputL * ll + inputR * lr;
            var dryR = inputL * rl + inputR * rr;
            var l = dryL; var r = dryR;
            for (var b = 0; b < 10; b++)
            { l = left[b].Process(l, coefficients[b]); r = right[b].Process(r, coefficients[b]); }
            l = (dryL + (l - dryL) * eqWet) * gain * headroom;
            r = (dryR + (r - dryR) * eqWet) * gain * headroom;
            delay[position * 2] = Limit(l); delay[position * 2 + 1] = Limit(r);
            var blend = Math.Min(1, delayFade);
            stereo[p] = (float)(Tap(oldDelay, 0) * (1 - blend) + Tap(currentDelay, 0) * blend);
            stereo[p + 1] = (float)(Tap(oldDelay, 1) * (1 - blend) + Tap(currentDelay, 1) * blend);
            blockPeak = Math.Max(blockPeak, Math.Max(Math.Abs(stereo[p]), Math.Abs(stereo[p + 1])));
            delayFade = Math.Min(1, delayFade + 1.0 / (rate * 0.03));
            position = (position + 1) % (delay.Length / 2);
        }
        Volatile.Write(ref peak, blockPeak);
    }
    private float Limit(double x)
    {
        if (!double.IsFinite(x)) { Interlocked.Increment(ref limited); return 0; }
        if (Math.Abs(x) > 1) Interlocked.Increment(ref limited);
        return (float)Math.Clamp(x, -1, 1);
    }
    private double Tap(double frames, int channel)
    {
        var capacity = delay.Length / 2;
        var index = position - frames;
        if (index < 0) index += capacity;
        var a = (int)index; var fraction = index - a;
        return delay[a * 2 + channel] * (1 - fraction) + delay[((a + 1) % capacity) * 2 + channel] * fraction;
    }
    public static (double LL, double LR, double RL, double RR) Matrix(SpeakerSettings s) => s.ChannelMode switch
    {
        ChannelMode.Left => (1, 0, 1, 0), ChannelMode.Right => (0, 1, 0, 1),
        ChannelMode.Mono => (0.5, 0.5, 0.5, 0.5),
        ChannelMode.Custom => (s.LeftMix / Math.Max(1, s.LeftMix + s.RightMix), s.RightMix / Math.Max(1, s.LeftMix + s.RightMix),
            s.LeftMix / Math.Max(1, s.LeftMix + s.RightMix), s.RightMix / Math.Max(1, s.LeftMix + s.RightMix)),
        _ => (1, 0, 0, 1)
    };
}
