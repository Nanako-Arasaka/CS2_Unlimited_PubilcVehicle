using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Policies;
using Game.Prefabs;
using Game.Routes;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VehicleCapUnlock.Core;

namespace VehicleCapUnlock.Systems
{
    // The game saturates normalized input. Extend both ranges along the same
    // line to keep new adjustments in range and preserve existing adjustments.
    public partial class LineCeilingSystem : GameSystemBase
    {
        private static readonly HashSet<LineCeilingSystem> s_Instances = new HashSet<LineCeilingSystem>();
        private bool m_PendingApply = true;
        private bool m_Active = true;
        private bool m_Captured;
        private Entity m_Policy;
        private PolicyPrefab m_PolicyPrefab;
        private PolicySliderData m_OriginalSlider;
        private RouteModifierData m_OriginalModifier;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_ConfigQuery;
        private EntityQuery m_LineQuery;

        public static void RequestReapply()
        {
            foreach (var system in s_Instances) system.m_PendingApply = true;
        }

        public static void ResumeAll()
        {
            foreach (var system in s_Instances)
            {
                system.m_Active = true;
                system.m_PendingApply = true;
            }
        }

        public static void RestoreAllNow()
        {
            foreach (var system in s_Instances)
            {
                system.m_Active = false;
                system.RestoreNow();
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ConfigQuery = GetEntityQuery(ComponentType.ReadOnly<UITransportConfigurationData>());
            m_LineQuery = GetEntityQuery(ComponentType.ReadOnly<Policy>(), ComponentType.ReadWrite<RouteModifier>());
            s_Instances.Add(this);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game) m_PendingApply = true;
            // Keep baselines for this World's lifetime, even across city loads.
        }

        protected override void OnUpdate()
        {
            if (!m_Active || !m_PendingApply) return;
            m_PendingApply = false;
            try { m_PendingApply = !Apply(); }
            catch (Exception e) { Mod.s_Log.Error(e, "LineCeilingSystem.Apply 失败"); }
        }

        protected override void OnDestroy()
        {
            RestoreNow();
            s_Instances.Remove(this);
            base.OnDestroy();
        }

        private bool Apply()
        {
            if (m_ConfigQuery.IsEmptyIgnoreFilter) return false;
            var config = m_PrefabSystem.GetSingletonPrefab<UITransportConfigurationPrefab>(m_ConfigQuery);
            if (config == null || config.m_VehicleCountPolicy == null) return false;
            var prefab = config.m_VehicleCountPolicy;
            Entity policy = m_PrefabSystem.GetEntity(prefab);
            if (policy == Entity.Null) return false;
            if (!EntityManager.HasComponent<PolicySliderData>(policy) ||
                !EntityManager.HasBuffer<RouteModifierData>(policy)) return false;

            EntityManager.CompleteAllTrackedJobs();
            if (m_Captured && (m_Policy != policy || !ReferenceEquals(m_PolicyPrefab, prefab)))
            {
                RestoreNow();
                m_Captured = false;
            }

            var slider = EntityManager.GetComponentData<PolicySliderData>(policy);
            var buffer = EntityManager.GetBuffer<RouteModifierData>(policy);
            int index = FindVehicleInterval(buffer);
            if (index < 0)
            {
                Mod.s_Log.Warn("VehicleCountPolicy 缺少 VehicleInterval，跳过");
                return true;
            }
            if (!m_Captured)
            {
                m_Policy = policy;
                m_PolicyPrefab = prefab;
                m_OriginalSlider = slider;
                m_OriginalModifier = buffer[index];
                m_Captured = true;
                Mod.s_Log.Info($"[基线] slider={slider.m_Range.min:F4}..{slider.m_Range.max:F4} " +
                    $"default={slider.m_Default:F4} | modifier={m_OriginalModifier.m_Range.min:F6}.." +
                    $"{m_OriginalModifier.m_Range.max:F6} mode={m_OriginalModifier.m_Mode}");
            }

            var setting = Mod.s_Setting;
            int multiplier = setting == null ? 1 : Math.Max(1, Math.Min(10, setting.VehicleLimitMultiplier));
            if (setting == null || !setting.EnableUnlock || multiplier == 1)
            {
                RestoreNow();
                return true;
            }

            CeilingMode mode;
            switch (m_OriginalModifier.m_Mode)
            {
                case ModifierValueMode.InverseRelative: mode = CeilingMode.InverseRelative; break;
                case ModifierValueMode.Relative: mode = CeilingMode.Relative; break;
                default:
                    RestoreNow();
                    Mod.s_Log.Warn($"不支持 {m_OriginalModifier.m_Mode} 的统一出车倍率语义，保持初始范围");
                    return true;
            }

            if (!CeilingMath.TryExtend(
                m_OriginalSlider.m_Range.min, m_OriginalSlider.m_Range.max,
                m_OriginalModifier.m_Range.min, m_OriginalModifier.m_Range.max,
                multiplier, mode, out float sliderMax, out float modifierMax, out string error))
            {
                RestoreNow();
                Mod.s_Log.Warn($"无法安全扩展配车上限：{error}");
                return true;
            }

            slider = EntityManager.GetComponentData<PolicySliderData>(policy);
            float previousSliderMax = slider.m_Range.max;
            slider.m_Range = new Bounds1(m_OriginalSlider.m_Range.min, sliderMax);
            EntityManager.SetComponentData(policy, slider);
            buffer = EntityManager.GetBuffer<RouteModifierData>(policy);
            index = FindVehicleInterval(buffer);
            var modifier = buffer[index];
            modifier.m_Range = new Bounds1(m_OriginalModifier.m_Range.min, modifierMax);
            buffer[index] = modifier;
            RefreshAffectedLines(Math.Min(previousSliderMax, sliderMax));
            Mod.s_Log.Info($"[应用] 原版最大配车水平 x{multiplier} | slider=" +
                $"{slider.m_Range.min:F4}..{sliderMax:F4} | modifier=" +
                $"{modifier.m_Range.min:F6}..{modifierMax:F6}");
            return true;
        }

        // Synchronous: does not rely on another simulation tick after disposal.
        private void RestoreNow()
        {
            if (!m_Captured) return;
            try
            {
                if (!World.IsCreated || !EntityManager.Exists(m_Policy) ||
                    !EntityManager.HasComponent<PolicySliderData>(m_Policy) ||
                    !EntityManager.HasBuffer<RouteModifierData>(m_Policy) ||
                    !ReferenceEquals(m_PrefabSystem.GetPrefab<PolicyPrefab>(m_Policy), m_PolicyPrefab)) return;
                EntityManager.CompleteAllTrackedJobs();
                var slider = EntityManager.GetComponentData<PolicySliderData>(m_Policy);
                float previousSliderMax = slider.m_Range.max;
                bool changed = slider.m_Range.min != m_OriginalSlider.m_Range.min ||
                    slider.m_Range.max != m_OriginalSlider.m_Range.max;
                if (changed)
                {
                    slider.m_Range = m_OriginalSlider.m_Range;
                    EntityManager.SetComponentData(m_Policy, slider);
                }
                var buffer = EntityManager.GetBuffer<RouteModifierData>(m_Policy);
                int index = FindVehicleInterval(buffer);
                if (index >= 0)
                {
                    var modifier = buffer[index];
                    changed |= modifier.m_Range.min != m_OriginalModifier.m_Range.min ||
                        modifier.m_Range.max != m_OriginalModifier.m_Range.max;
                    modifier.m_Range = m_OriginalModifier.m_Range;
                    buffer[index] = modifier;
                }
                if (changed)
                {
                    RefreshAffectedLines(Math.Min(previousSliderMax, m_OriginalSlider.m_Range.max));
                    Mod.s_Log.Info("[还原] VehicleCountPolicy 已同步恢复初始范围");
                }
            }
            catch (Exception e) { Mod.s_Log.Error(e, "线路范围同步还原失败"); }
        }

        // Only adjustments beyond the unchanged domain can have a stale cached
        // effect. Recompute only VehicleInterval, preserving other modifier slots
        // and the player's stored per-line adjustment/active flag.
        private void RefreshAffectedLines(float unchangedMaximum)
        {
            var sliders = GetComponentLookup<PolicySliderData>(true);
            int refreshed = 0;
            using (var entities = m_LineQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity line in entities)
                {
                    var policies = EntityManager.GetBuffer<Policy>(line, true);
                    bool affected = false;
                    foreach (var policy in policies)
                    {
                        if (policy.m_Policy == m_Policy && (policy.m_Flags & PolicyFlags.Active) != 0 &&
                            policy.m_Adjustment > unchangedMaximum) { affected = true; break; }
                    }
                    if (!affected) continue;

                    RouteModifier value = default;
                    foreach (var policy in policies)
                    {
                        if ((policy.m_Flags & PolicyFlags.Active) == 0 ||
                            !EntityManager.HasBuffer<RouteModifierData>(policy.m_Policy)) continue;
                        var data = EntityManager.GetBuffer<RouteModifierData>(policy.m_Policy, true);
                        foreach (var item in data)
                        {
                            if (item.m_Type != RouteModifierType.VehicleInterval) continue;
                            float delta = RouteModifierInitializeSystem.RouteModifierRefreshData.GetModifierDelta(
                                item, policy.m_Adjustment, policy.m_Policy, sliders);
                            RouteModifierInitializeSystem.RouteModifierRefreshData.AddModifierData(ref value, item, delta);
                        }
                    }

                    var modifiers = EntityManager.GetBuffer<RouteModifier>(line);
                    int index = (int)RouteModifierType.VehicleInterval;
                    while (modifiers.Length <= index) modifiers.Add(default);
                    modifiers[index] = value;
                    refreshed++;
                }
            }
            if (refreshed > 0) Mod.s_Log.Info($"[线路缓存] 已刷新 {refreshed} 条线路的 VehicleInterval");
        }

        private static int FindVehicleInterval(DynamicBuffer<RouteModifierData> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                if (buffer[i].m_Type == RouteModifierType.VehicleInterval) return i;
            return -1;
        }
    }
}
