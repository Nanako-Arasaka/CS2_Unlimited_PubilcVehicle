using System;
using VehicleCapUnlock.Core;

internal static class Program
{
    private static int s_Assertions;

    private static int Main()
    {
        try
        {
            // Synthetic fixtures, NOT claims about the game's runtime prefab values.
            CheckMapping(50f, 200f, -0.5f, 1f, CeilingMode.InverseRelative);
            CheckMapping(50f, 200f, -1f, 0f, CeilingMode.InverseRelative);
            CheckMapping(-20f, 80f, 0.5f, -0.5f, CeilingMode.Relative);
            var random = new Random(172026);
            for (int i = 0; i < 200; i++)
            {
                float sliderMin = (float)(random.NextDouble() * 100 - 50);
                float sliderMax = sliderMin + 25f + (float)random.NextDouble() * 500f;
                float modifierMin = -0.9f + (float)random.NextDouble() * 0.5f;
                float modifierMax = 0.1f + (float)random.NextDouble() * 4f;
                CheckMapping(sliderMin, sliderMax, modifierMin, modifierMax, CeilingMode.InverseRelative);
                CheckMapping(sliderMin, sliderMax, modifierMax, modifierMin, CeilingMode.Relative);
            }

            Reject(0, 0, -0.5f, 1, 5, CeilingMode.InverseRelative);
            Reject(float.NaN, 200, -0.5f, 1, 5, CeilingMode.InverseRelative);
            Reject(50, float.PositiveInfinity, -0.5f, 1, 5, CeilingMode.InverseRelative);
            Reject(50, 200, -0.5f, float.NaN, 5, CeilingMode.InverseRelative);
            Reject(50, 200, -0.5f, 1, 0, CeilingMode.InverseRelative);
            Reject(50, 200, -0.5f, 1, 11, CeilingMode.InverseRelative);
            Reject(50, 200, 1, -0.5f, 5, CeilingMode.InverseRelative);
            Reject(50, 200, -0.5f, 1, 5, CeilingMode.Relative);
            Reject(50, 200, 0, 0, 5, CeilingMode.InverseRelative);
            Reject(50, 200, 0, -1, 5, CeilingMode.Relative);
            Reject(50, 200, -0.5f, 1, 5, (CeilingMode)99);
            Console.WriteLine($"PASS: {s_Assertions} assertions; original-domain mapping, maximum-based multiplier, inverse conversion, both relative modes, bounds and finite values.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void CheckMapping(float min, float max, float modMin, float modMax, CeilingMode mode)
    {
        for (int k = 1; k <= 10; k++)
        {
            bool ok = CeilingMath.TryExtend(min, max, modMin, modMax, k, mode,
                out float newMax, out float newModMax, out string error);
            Assert(ok, error ?? "Valid mapping rejected");
            if (k == 1)
            {
                Assert(max == newMax && modMax == newModMax, "1x must preserve exact original bounds");
                continue;
            }
            Assert(newMax > max, "Slider maximum did not increase");
            double oldVehicles = VehicleFactor(modMax, mode);
            double newVehicles = VehicleFactor(newModMax, mode);
            Near(newVehicles / oldVehicles, k, "Multiplier must be relative to the original maximum");
            // Includes below-min input to exercise the real game's saturate behavior.
            for (int i = -1; i <= 20; i++)
            {
                float adjustment = min + (max - min) * i / 20f;
                double before = Map(adjustment, min, max, modMin, modMax);
                double after = Map(adjustment, min, newMax, modMin, newModMax);
                Near(after, before, "Original adjustment changed");
            }
            for (int i = 0; i <= 20; i++)
            {
                double adjustment = min + ((double)newMax - min) * i / 20;
                double delta = Map(adjustment, min, newMax, modMin, newModMax);
                double roundTrip = min + (delta - modMin) * (newMax - min) / (newModMax - modMin);
                Near(roundTrip, adjustment, "Vanilla inverse conversion round trip failed");
            }
        }
    }

    private static double Map(double input, float min, float max, float modMin, float modMax)
    {
        double a = Math.Max(0, Math.Min(1, (input - min) / ((double)max - min)));
        return modMin + ((double)modMax - modMin) * a;
    }

    private static double VehicleFactor(float delta, CeilingMode mode) =>
        mode == CeilingMode.InverseRelative ? Math.Max(0.001, 1d + delta) : 1d / (1d + delta);

    private static void Reject(float min, float max, float modMin, float modMax, int k, CeilingMode mode)
    {
        Assert(!CeilingMath.TryExtend(min, max, modMin, modMax, k, mode,
            out _, out _, out string error), "Unsafe mapping accepted");
        Assert(!string.IsNullOrWhiteSpace(error), "Rejection requires an explanation");
    }

    private static void Near(double actual, double expected, string message)
    {
        Assert(Math.Abs(actual - expected) <= 0.00001 * Math.Max(1, Math.Abs(expected)),
            $"{message}: expected={expected:R}, actual={actual:R}");
    }

    private static void Assert(bool condition, string message)
    {
        s_Assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
