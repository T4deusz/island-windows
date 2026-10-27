using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Island;
/// <summary>Response is the natural period in seconds; Damping is the damping ratio (1 is critical damping).</summary>
internal readonly record struct SpringConfig(double Response, double Damping);

internal static class Springs
{
    internal static readonly SpringConfig Open = new(.46, .78);
    internal static readonly SpringConfig Close = new(.38, .96);
    internal static readonly SpringConfig Smooth = new(.42, 1);
    internal static readonly SpringConfig Snappy = new(.30, .88);
    internal static readonly SpringConfig Bouncy = new(.46, .70);
    internal static readonly SpringConfig Tracking = new(.16, 1);
    internal static readonly SpringConfig Release = new(.42, .72);
}

internal static class Ease
{
    internal static double Smoothstep(double from, double to, double x) { double t = Math.Clamp((x - from) / (to - from), 0, 1); return t * t * (3 - 2 * t); }
}

internal sealed class Spring
{
    internal double Value, Velocity, Target;
    internal bool Active => Math.Abs(Value - Target) > .0002 || Math.Abs(Velocity) > .002;
    internal Spring(double value = 0) { Value = Target = value; }
    internal void Snap(double target) { Value = Target = target; Velocity = 0; }
    internal void Step(double elapsed, SpringConfig config)
    {
        // Small integration steps keep the same spring stable across frame stalls and refresh rates.
        elapsed = Math.Clamp(elapsed, 0, .1);
        double omega = 2 * Math.PI / Math.Max(.05, config.Response), stiffness = omega * omega, damping = 2 * config.Damping * omega;
        while (elapsed > 0) { double dt = Math.Min(elapsed, 1d / 240); Velocity += (stiffness * (Target - Value) - damping * Velocity) * dt; Value += Velocity * dt; elapsed -= dt; }
        if (!Active) Snap(Target);
    }
    internal static double RubberBand(double pixels, double limit = 120) => Math.Sign(pixels) * (1 - 1 / (Math.Abs(pixels) * .55 / limit + 1)) * limit;
}
internal static class ArtworkPalette
{
    internal static Color Extract(BitmapSource source)
    {
        var small = new TransformedBitmap(source, new ScaleTransform(32d / source.PixelWidth, 32d / source.PixelHeight));
        var bitmap = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[32 * 32 * 4]; bitmap.CopyPixels(pixels, 32 * 4, 0);
        double[] weights = new double[512], red = new double[512], green = new double[512], blue = new double[512];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            double r = pixels[i + 2] / 255d, g = pixels[i + 1] / 255d, b = pixels[i] / 255d;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            if (pixels[i + 3] < 128 || max < .12 || min > .93) continue;
            double weight = .15 + (max - min) * 2;
            int bin = (pixels[i + 2] >> 5) * 64 + (pixels[i + 1] >> 5) * 8 + (pixels[i] >> 5);
            weights[bin] += weight; red[bin] += r * weight; green[bin] += g * weight; blue[bin] += b * weight;
        }
        int best = 0; for (int i = 1; i < 512; i++) if (weights[i] > weights[best]) best = i;
        if (weights[best] == 0) return Color.FromRgb(131, 226, 198);
        double rr = red[best] / weights[best], gg = green[best] / weights[best], bb = blue[best] / weights[best];
        double luminance = .2126 * rr + .7152 * gg + .0722 * bb;
        double lift = Math.Clamp((.58 - luminance) / Math.Max(.01, 1 - luminance), 0, .65);
        return Color.FromRgb((byte)((rr + (1 - rr) * lift) * 255), (byte)((gg + (1 - gg) * lift) * 255), (byte)((bb + (1 - bb) * lift) * 255));
    }
}
