using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Policies;
using Game.Prefabs;
using Game.Routes;
using System;
using Unity.Entities;
using Unity.Mathematics;

namespace VehicleCapUnlock.Systems
{
    /// <summary>
    /// 抬高每条线路「配车数量」滑块的上限，但保持原版映射关系不变。
    ///
    /// 原理（全部来自反编译源码核对）：
    ///   slider 空间 —— PolicySliderData.m_Range (min..max) + m_Default
    ///   modifier 空间 —— RouteModifierData[VehicleInterval].m_Range (min..max) + m_Mode
    ///   换算 —— RouteModifierInitializeSystem.GetModifierDelta:
    ///               a = (adjustment - sliderMin) / (sliderMax - sliderMin)
    ///               L = lerp(modMin, modMax, a)
    ///   生效 —— AddModifierData(InverseRelative): L' = 1/max(0.001, 1+L) - 1
    ///           最终间隔 = defaultInterval * (1 + L')
    ///           即「出车倍率」= 1 / (1 + L') = max(0.001, 1 + L)
    ///
    /// 关键点：GetModifierDelta 是纯线性的 lerp，不 clamp。所以只要把
    /// (sliderMin, modMin) 这个锚点保持不变、把 (sliderMax, modMax) 沿同一条直线向外推，
    /// 原版区间内任何一个 adjustment 的效果都丝毫不变，只有超出原版上限的部分是新增的。
    /// 这就是「和原版一样，只是能拉得更远」。
    /// </summary>
    public partial class LineCeilingSystem : GameSystemBase
    {
        private static bool s_PendingApply = true;
        private static bool s_PendingRestore;

        private static bool s_Captured;
        private static Bounds1 s_VanillaSlider;
        private static float s_VanillaDefault;
        private static Bounds1 s_VanillaModifier;
        private static ModifierValueMode s_VanillaMode;

        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_ConfigQuery;

        public static void RequestReapply() { s_PendingApply = true; }
        public static void RequestRestore() { s_PendingRestore = true; }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ConfigQuery = GetEntityQuery(ComponentType.ReadOnly<UITransportConfigurationData>());
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game && (purpose == Purpose.NewGame || purpose == Purpose.LoadGame))
            {
                s_Captured = false;   // 新会话重新采集原版基线
            }
            s_PendingApply = true;
        }

        protected override void OnUpdate()
        {
            if (!s_PendingApply && !s_PendingRestore) return;

            bool restore = s_PendingRestore;
            s_PendingApply = false;
            s_PendingRestore = false;

            try
            {
                Apply(restore);
            }
            catch (Exception e)
            {
                Mod.s_Log.Error(e, "LineCeilingSystem.Apply 失败");
            }
        }

        private void Apply(bool restore)
        {
            Entity policy = ResolvePolicyEntity();
            if (policy == Entity.Null)
            {
                s_PendingApply = true;   // prefab 还没就绪，下一帧再试
                return;
            }

            if (!EntityManager.HasComponent<PolicySliderData>(policy) ||
                !EntityManager.HasBuffer<RouteModifierData>(policy))
            {
                Mod.s_Log.Warn("VehicleCountPolicy 缺少 PolicySliderData 或 RouteModifierData，跳过");
                return;
            }

            PolicySliderData slider = EntityManager.GetComponentData<PolicySliderData>(policy);
            DynamicBuffer<RouteModifierData> buffer = EntityManager.GetBuffer<RouteModifierData>(policy);

            int idx = -1;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].m_Type == RouteModifierType.VehicleInterval) { idx = i; break; }
            }

            if (idx < 0)
            {
                Mod.s_Log.Warn("VehicleCountPolicy 里找不到 VehicleInterval 条目，跳过");
                return;
            }

            if (!s_Captured)
            {
                s_VanillaSlider = slider.m_Range;
                s_VanillaDefault = slider.m_Default;
                s_VanillaModifier = buffer[idx].m_Range;
                s_VanillaMode = buffer[idx].m_Mode;
                s_Captured = true;

                Mod.s_Log.Info(
                    $"[基线] slider={s_VanillaSlider.min:F1}..{s_VanillaSlider.max:F1} " +
                    $"default={s_VanillaDefault:F1} | modifier={s_VanillaModifier.min:F4}..{s_VanillaModifier.max:F4} " +
                    $"mode={s_VanillaMode}");
            }

            Setting s = Mod.s_Setting;
            int multiplier = s != null ? s.VehicleLimitMultiplier : 1;
            bool enable = s == null || s.EnableUnlock;

            float k = math.clamp(multiplier, 1, 10);

            if (restore || !enable || k <= 1f)
            {
                RestoreVanilla(policy, buffer, idx, slider);
                return;
            }

            float span = s_VanillaSlider.max - s_VanillaSlider.min;
            float modSpan = s_VanillaModifier.max - s_VanillaModifier.min;

            if (span <= 0f || modSpan == 0f)
            {
                Mod.s_Log.Warn($"原版范围退化（span={span:F3}, modSpan={modSpan:F4}），无法安全外推");
                return;
            }

            float a0 = math.clamp((s_VanillaDefault - s_VanillaSlider.min) / span, 0f, 1f);
            float l0 = s_VanillaModifier.min + modSpan * a0;
            float slope = modSpan / span;

            float lTarget;
            switch (s_VanillaMode)
            {
                case ModifierValueMode.InverseRelative:
                {
                    // 出车倍率 = max(0.001, 1 + L)
                    float scale0 = math.max(0.001f, 1f + l0);
                    lTarget = k * scale0 - 1f;
                    break;
                }
                case ModifierValueMode.Relative:
                {
                    // 间隔倍率 = 1 + L，出车倍率 = 1 / (1 + L)
                    float factor0 = 1f + l0;
                    lTarget = factor0 > 0.001f ? factor0 / k - 1f : s_VanillaModifier.max;
                    break;
                }
                default:
                {
                    // Absolute 是「给间隔加固定秒数」，无法表达倍率语义，退化为线性外推
                    Mod.s_Log.Warn($"modifier 模式为 {s_VanillaMode}，只能线性外推，倍率不精确");
                    lTarget = s_VanillaModifier.min + slope * k * span;
                    break;
                }
            }

            if (1f + lTarget <= 0.001f)
            {
                lTarget = -0.999f;
            }

            if (lTarget <= s_VanillaModifier.max + 1e-4f)
            {
                Mod.s_Log.Info($"目标 {lTarget:F4} 未超过原版上限 {s_VanillaModifier.max:F4}，保持原版");
                RestoreVanilla(policy, buffer, idx, slider);
                return;
            }

            float newSliderMax = s_VanillaSlider.min + (lTarget - s_VanillaModifier.min) / slope;

            if (newSliderMax <= s_VanillaSlider.max || newSliderMax > 100000f)
            {
                Mod.s_Log.Warn($"推算出的滑块上限 {newSliderMax:F1} 不合理，放弃");
                return;
            }

            slider.m_Range = new Bounds1(s_VanillaSlider.min, newSliderMax);
            EntityManager.SetComponentData(policy, slider);

            RouteModifierData item = buffer[idx];
            item.m_Range = new Bounds1(s_VanillaModifier.min, lTarget);
            buffer[idx] = item;

            Mod.s_Log.Info(
                $"[应用] x{k} | slider {s_VanillaSlider.min:F1}..{newSliderMax:F1} " +
                $"(原 {s_VanillaSlider.min:F1}..{s_VanillaSlider.max:F1}) | " +
                $"modifier {s_VanillaModifier.min:F4}..{lTarget:F4} (原 ..{s_VanillaModifier.max:F4})");
        }

        private void RestoreVanilla(
            Entity policy, DynamicBuffer<RouteModifierData> buffer, int idx, PolicySliderData slider)
        {
            if (!s_Captured) return;

            bool dirty = false;

            if (slider.m_Range.min != s_VanillaSlider.min || slider.m_Range.max != s_VanillaSlider.max)
            {
                slider.m_Range = s_VanillaSlider;
                EntityManager.SetComponentData(policy, slider);
                dirty = true;
            }

            RouteModifierData item = buffer[idx];
            if (item.m_Range.min != s_VanillaModifier.min || item.m_Range.max != s_VanillaModifier.max)
            {
                item.m_Range = s_VanillaModifier;
                buffer[idx] = item;
                dirty = true;
            }

            if (dirty)
            {
                Mod.s_Log.Info($"[还原] slider={s_VanillaSlider.min:F1}..{s_VanillaSlider.max:F1}, " +
                               $"modifier={s_VanillaModifier.min:F4}..{s_VanillaModifier.max:F4}");
            }
        }

        private Entity ResolvePolicyEntity()
        {
            if (m_ConfigQuery.IsEmptyIgnoreFilter) return Entity.Null;

            UITransportConfigurationPrefab config =
                m_PrefabSystem.GetSingletonPrefab<UITransportConfigurationPrefab>(m_ConfigQuery);

            if (config == null || config.m_VehicleCountPolicy == null) return Entity.Null;

            return m_PrefabSystem.GetEntity(config.m_VehicleCountPolicy);
        }
    }
}
