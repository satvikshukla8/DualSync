using System.Numerics;

namespace DualSync.Core;

// RBJ peaking-EQ equations, independently implemented from the W3C Audio EQ Cookbook.
public readonly record struct BiquadCoefficients(double B0, double B1, double B2, double A1, double A2)
{
    public static BiquadCoefficients Peak(double rate, double frequency, double gainDb, double q = 1.4)
    {
        if (rate <= 0 || !double.IsFinite(rate) || frequency <= 0 || frequency >= rate / 2 || q <= 0 || !double.IsFinite(q) || !double.IsFinite(gainDb))
            throw new ArgumentOutOfRangeException(nameof(frequency));
        var a = Math.Pow(10, gainDb / 40);
        var w = 2 * Math.PI * frequency / rate;
        var alpha = Math.Sin(w) / (2 * q);
        var a0 = 1 + alpha / a;
        return new((1 + alpha * a) / a0, -2 * Math.Cos(w) / a0, (1 - alpha * a) / a0,
            -2 * Math.Cos(w) / a0, (1 - alpha / a) / a0);
    }
    public double Magnitude(double rate, double frequency)
    {
        var z = Complex.FromPolarCoordinates(1, -2 * Math.PI * frequency / rate);
        return ((B0 + B1 * z + B2 * z * z) / (1 + A1 * z + A2 * z * z)).Magnitude;
    }
    public bool IsStable => Math.Abs(A2) < 1 && 1 + A1 + A2 > 0 && 1 - A1 + A2 > 0;
}

internal struct BiquadState
{
    private double x1, x2, y1, y2;
    public double Process(double x, BiquadCoefficients c)
    {
        var y = c.B0 * x + c.B1 * x1 + c.B2 * x2 - c.A1 * y1 - c.A2 * y2;
        x2 = x1; x1 = x; y2 = y1; y1 = Math.Abs(y) < 1e-25 ? 0 : y;
        return y1;
    }
}
