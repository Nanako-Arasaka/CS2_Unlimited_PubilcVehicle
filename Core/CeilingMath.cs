using System;

namespace VehicleCapUnlock.Core
{
    internal enum CeilingMode { InverseRelative, Relative }

    // No game dependencies: tested by the standalone regression runner.
    internal static class CeilingMath
    {
        public static bool TryExtend(
            float sliderMin, float sliderMax, float modifierMin, float modifierMax,
            int multiplier, CeilingMode mode,
            out float extendedSliderMax, out float extendedModifierMax, out string error)
        {
            extendedSliderMax = sliderMax;
            extendedModifierMax = modifierMax;
            error = null;
            if (!IsFinite(sliderMin) || !IsFinite(sliderMax) ||
                !IsFinite(modifierMin) || !IsFinite(modifierMax) || sliderMax <= sliderMin)
            {
                error = "滑块或 modifier 范围无效";
                return false;
            }
            if (multiplier < 1 || multiplier > 10)
            {
                error = "倍率必须在 1 到 10 之间";
                return false;
            }
            if (multiplier == 1) return true;

            double span = (double)sliderMax - sliderMin;
            double modifierSpan = (double)modifierMax - modifierMin;
            double originalFactor = 1d + modifierMax;
            double target;
            switch (mode)
            {
                case CeilingMode.InverseRelative:
                    if (modifierSpan <= 0d || originalFactor <= 0.001d)
                    {
                        error = "InverseRelative 范围不能安全提高出车上限";
                        return false;
                    }
                    // Relative to the original MAXIMUM, not the default adjustment.
                    target = multiplier * originalFactor - 1d;
                    break;
                case CeilingMode.Relative:
                    if (modifierSpan >= 0d || originalFactor <= 0d)
                    {
                        error = "Relative 范围不能安全提高出车上限";
                        return false;
                    }
                    target = originalFactor / multiplier - 1d;
                    break;
                default:
                    error = "不支持的 modifier 模式";
                    return false;
            }

            float newModifierMax = (float)target;
            float newSliderMax = (float)(sliderMin +
                ((double)newModifierMax - modifierMin) * span / modifierSpan);
            if (!IsFinite(newSliderMax) || !IsFinite(newModifierMax) ||
                newSliderMax <= sliderMax || 1f + newModifierMax <= 0f)
            {
                error = "推算结果溢出、精度不足或没有扩展上限";
                return false;
            }
            extendedSliderMax = newSliderMax;
            extendedModifierMax = newModifierMax;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
