using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Policies;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VehicleCapUnlock.Core;

namespace VehicleCapUnlock.Systems
{
    public partial class LineCeilingSystem : GameSystemBase
    {
        private readonly struct PolicyShape : IEquatable<PolicyShape>
        {
            public readonly PolicySliderData Slider;
            public readonly RouteModifierData Modifier;
            public PolicyShape(PolicySliderData slider, RouteModifierData modifier) { Slider = slider; Modifier = modifier; }
            public bool Equals(PolicyShape other) => Slider.Equals(other.Slider) && Modifier.Equals(other.Modifier);
            public override bool Equals(object obj) => obj is PolicyShape other && Equals(other);
            public override int GetHashCode() => Slider.GetHashCode() ^ Modifier.GetHashCode();
        }

        private static readonly HashSet<LineCeilingSystem> s_Instances = new HashSet<LineCeilingSystem>();
        private readonly PendingWork m_Work = new PendingWork();
        private bool m_Active = true;
        private bool m_GameLoaded;
        private bool m_PrepareUnload;
        private bool m_UnsafeFailure;
        private string m_Status = "NoCity";
        private Entity m_Policy;
        private PolicyPrefab m_PolicyPrefab;
        private OwnedValue<PolicyShape> m_Owned;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_ConfigQuery;
        private EntityQuery m_LineQuery;
        private EntityQuery m_CreatedLineQuery;

        public static string StatusKey
        {
            get { foreach (var s in s_Instances) if (s.m_GameLoaded && s.m_Active) return s.m_Status; return "NoCity"; }
        }

        public static bool HasLoadedCity
        {
            get { foreach (var s in s_Instances) if (s.m_GameLoaded && s.m_Active) return true; return false; }
        }

        public static void RequestReapply()
        {
            foreach (var s in s_Instances)
            {
                s.m_PrepareUnload = false;
                s.m_Work.Request();
                s.m_Status = s.m_UnsafeFailure ? "Failed" : "Pending";
            }
        }

        public static void RequestPrepareUnload()
        {
            foreach (var s in s_Instances) if (s.m_GameLoaded && s.m_Active)
            { s.m_PrepareUnload = true; s.m_Work.Request(); s.m_Status = s.m_UnsafeFailure ? "Failed" : "Pending"; }
        }

        public static void ResumeAll()
        {
            foreach (var s in s_Instances) { s.m_Active = true; s.m_Work.Request(); }
        }

        public static bool RestoreAllNow()
        {
            bool complete = true;
            foreach (var s in s_Instances) { s.m_Active = false; complete &= s.TryRestore(); }
            return complete;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ConfigQuery = GetEntityQuery(ComponentType.ReadOnly<UITransportConfigurationData>());
            m_LineQuery = GetEntityQuery(ComponentType.ReadOnly<Route>(), ComponentType.ReadOnly<TransportLine>(),
                ComponentType.ReadWrite<Policy>(), ComponentType.ReadWrite<RouteModifier>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Created>());
            m_CreatedLineQuery = GetEntityQuery(ComponentType.ReadOnly<Route>(), ComponentType.ReadOnly<TransportLine>(),
                ComponentType.ReadOnly<Policy>(), ComponentType.ReadOnly<Created>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>());
            s_Instances.Add(this);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_GameLoaded = mode == GameMode.Game;
            m_PrepareUnload = false;
            m_Work.Request();
            m_Status = m_UnsafeFailure ? "Failed" : m_GameLoaded ? "Pending" : "NoCity";
        }

        protected override void OnUpdate()
        {
            if (!m_Active || !m_GameLoaded || !m_Work.Pending || m_UnsafeFailure) return;
            // Let the game's initializer finish before projecting loaded/new route caches.
            if (!m_CreatedLineQuery.IsEmptyIgnoreFilter) return;
            try { if (Apply()) m_Work.Complete(); }
            catch (Exception e)
            {
                bool rollbackComplete = !(e is WriteBatchException batch) || batch.RollbackSucceeded;
                m_UnsafeFailure |= !rollbackComplete;
                m_Work.Fail(rollbackComplete);
                m_Status = m_Work.Failed ? "Failed" : "Pending";
                Mod.s_Log.Error(e, $"[线路失败] attempt={m_Work.Attempts}; retry={m_Work.Pending}; rollback={rollbackComplete}");
            }
        }

        protected override void OnDestroy()
        {
            TryRestore();
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
            if (!EntityManager.Exists(policy) || !EntityManager.HasComponent<PolicySliderData>(policy) ||
                !EntityManager.HasBuffer<RouteModifierData>(policy)) return false;
            EntityManager.CompleteAllTrackedJobs();
            if (m_Owned != null && (m_Policy != policy || !ReferenceEquals(m_PolicyPrefab, prefab)))
            {
                if (!TryRestore()) throw new InvalidOperationException("Previous policy could not be restored");
                m_Owned = null;
            }
            m_Policy = policy;
            m_PolicyPrefab = prefab;
            PolicyShape current = ReadShape();
            if (m_Owned == null)
            {
                ValidateShape(current);
                m_Owned = new OwnedValue<PolicyShape>(current);
                Mod.s_Log.Info($"[基线] slider={current.Slider.m_Range.min}..{current.Slider.m_Range.max}; " +
                    $"modifier={current.Modifier.m_Range.min}..{current.Modifier.m_Range.max}; mode={current.Modifier.m_Mode}");
            }
            if (!m_Owned.Accepts(current))
            { Conflict("配车 policy 已被其他系统修改，跳过应用与还原"); return true; }

            var setting = Mod.s_Setting;
            int multiplier = !m_PrepareUnload && setting != null && setting.EnableUnlock
                ? Math.Max(1, Math.Min(10, setting.VehicleLimitMultiplier)) : 1;
            PolicyShape target = GetShape(multiplier);
            if (!RunChange(current, target, m_PrepareUnload)) return true;
            m_Status = m_PrepareUnload ? "Prepared" : "Applied";
            Mod.s_Log.Info($"[线路完成] multiplier={multiplier}; prepareUnload={m_PrepareUnload}");
            return true;
        }

        private bool TryRestore()
        {
            if (m_Owned == null || !World.IsCreated || !EntityManager.Exists(m_Policy)) return true;
            try
            {
                EntityManager.CompleteAllTrackedJobs();
                if (!m_CreatedLineQuery.IsEmptyIgnoreFilter)
                { Conflict("线路仍在初始化，未把跳过这些线路的还原报告为成功"); return false; }
                PolicyShape current = ReadShape();
                if (!m_Owned.Accepts(current)) { Conflict("配车 policy 已被其他系统修改，未覆盖其数据"); return false; }
                return RunChange(current, m_Owned.Original, false);
            }
            catch (Exception e) { m_Status = "Failed"; Mod.s_Log.Error(e, "[线路还原失败] 不应继续保存或卸载"); return false; }
        }

        private bool RunChange(PolicyShape before, PolicyShape after, bool normalize)
        {
            ValidateShape(after);
            var writes = new List<IReversibleWrite>();
            if (!before.Slider.Equals(after.Slider))
                writes.Add(new ValueWrite<PolicySliderData>(() => ReadShape().Slider,
                    value => EntityManager.SetComponentData(m_Policy, value), before.Slider, after.Slider));
            if (!before.Modifier.Equals(after.Modifier))
                writes.Add(new ValueWrite<RouteModifierData>(() => ReadShape().Modifier,
                    value => { var data = EntityManager.GetBuffer<RouteModifierData>(m_Policy); data[FindInterval(data)] = value; },
                    before.Modifier, after.Modifier));

            int refreshed = 0, normalized = 0;
            var sliders = GetComponentLookup<PolicySliderData>(true);
            using (var entities = m_LineQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity line in entities)
                {
                    var policies = EntityManager.GetBuffer<Policy>(line, true);
                    var saved = new Policy[policies.Length];
                    bool affected = false;
                    for (int i = 0; i < saved.Length; i++)
                    {
                        saved[i] = policies[i];
                        affected |= saved[i].m_Policy == m_Policy;
                    }
                    if (!affected) continue;
                    var modifiers = EntityManager.GetBuffer<RouteModifier>(line, true);
                    int slot = (int)RouteModifierType.VehicleInterval;
                    if (modifiers.Length <= slot) return Conflict("正式线路缺少 VehicleInterval 槽位，未修改范围或线路");
                    RouteModifier oldValue = modifiers[slot];
                    // Recognize caches from this mod's supported historical mappings.
                    // An unexplained direct write is a conflict, not permission to overwrite it.
                    if (!IsRecognizedCache(saved, oldValue, before, sliders))
                        return Conflict("线路缓存不符合当前或历史配车映射，未覆盖未知来源的修改");

                    var adjusted = (Policy[])saved.Clone();
                    for (int i = 0; i < adjusted.Length; i++)
                    {
                        if (!normalize || adjusted[i].m_Policy != m_Policy) continue;
                        float adjustment = adjusted[i].m_Adjustment;
                        if (!IsFinite(adjustment)) throw new InvalidOperationException("Non-finite saved policy adjustment");
                        adjusted[i].m_Adjustment = math.clamp(adjustment, after.Slider.m_Range.min, after.Slider.m_Range.max);
                        if (adjusted[i].Equals(saved[i])) continue;
                        int policySlot = i;
                        writes.Add(new ValueWrite<Policy>(() => EntityManager.GetBuffer<Policy>(line, true)[policySlot],
                            value => { var data = EntityManager.GetBuffer<Policy>(line); data[policySlot] = value; },
                            saved[i], adjusted[i]));
                        normalized++;
                    }
                    RouteModifier target = ProjectInterval(adjusted, after, sliders);
                    if (oldValue.Equals(target)) continue;
                    writes.Add(new ValueWrite<RouteModifier>(() => EntityManager.GetBuffer<RouteModifier>(line, true)[slot],
                        value => { var data = EntityManager.GetBuffer<RouteModifier>(line); data[slot] = value; }, oldValue, target));
                    refreshed++;
                }
            }
            // All projections and snapshots are ready before changing any ECS data.
            SafeWriteBatch.Execute(writes);
            m_Owned.Commit(after);
            Mod.s_Log.Info($"[线路校正] refreshed={refreshed}; normalized={normalized}; changes={writes.Count}");
            return true;
        }

        private bool IsRecognizedCache(Policy[] policies, RouteModifier value, PolicyShape current,
            ComponentLookup<PolicySliderData> sliders)
        {
            if (Near(value, ProjectInterval(policies, current, sliders))) return true;
            for (int k = 1; k <= 10; k++)
            {
                if (!TryGetShape(k, out var shape)) continue;
                if (Near(value, ProjectInterval(policies, shape, sliders))) return true;
            }
            return false;
        }

        private RouteModifier ProjectInterval(Policy[] policies, PolicyShape shape, ComponentLookup<PolicySliderData> sliders)
        {
            RouteModifier value = default;
            foreach (var policy in policies)
            {
                if ((policy.m_Flags & PolicyFlags.Active) == 0 || !EntityManager.HasBuffer<RouteModifierData>(policy.m_Policy)) continue;
                var data = EntityManager.GetBuffer<RouteModifierData>(policy.m_Policy, true);
                foreach (var entry in data)
                {
                    if (entry.m_Type != RouteModifierType.VehicleInterval) continue;
                    if (!IsFinite(policy.m_Adjustment)) throw new InvalidOperationException("Non-finite policy adjustment");
                    RouteModifierData item = policy.m_Policy == m_Policy ? shape.Modifier : entry;
                    float delta;
                    if (policy.m_Policy == m_Policy)
                    {
                        // Same saturate/lerp as the inspected game method, with planned ranges.
                        float a = math.saturate((policy.m_Adjustment - shape.Slider.m_Range.min) /
                            (shape.Slider.m_Range.max - shape.Slider.m_Range.min));
                        delta = math.lerp(item.m_Range.min, item.m_Range.max, a);
                    }
                    else delta = RouteModifierInitializeSystem.RouteModifierRefreshData.GetModifierDelta(item,
                        policy.m_Adjustment, policy.m_Policy, sliders);
                    if (!IsFinite(delta)) throw new InvalidOperationException("Non-finite modifier delta");
                    RouteModifierInitializeSystem.RouteModifierRefreshData.AddModifierData(ref value, item, delta);
                }
            }
            if (!IsFinite(value.m_Delta.x) || !IsFinite(value.m_Delta.y) || value.m_Delta.y <= -1f)
                throw new InvalidOperationException("Unsafe combined VehicleInterval modifier");
            return value;
        }

        private PolicyShape ReadShape()
        {
            if (!ReferenceEquals(m_PrefabSystem.GetPrefab<PolicyPrefab>(m_Policy), m_PolicyPrefab))
                throw new InvalidOperationException("Policy prefab identity changed");
            var data = EntityManager.GetBuffer<RouteModifierData>(m_Policy, true);
            return new PolicyShape(EntityManager.GetComponentData<PolicySliderData>(m_Policy), data[FindInterval(data)]);
        }

        private PolicyShape GetShape(int multiplier)
        {
            if (!TryGetShape(multiplier, out var shape)) throw new InvalidOperationException("Unsupported vehicle ceiling mapping");
            return shape;
        }

        private bool TryGetShape(int multiplier, out PolicyShape shape)
        {
            shape = m_Owned.Original;
            if (multiplier == 1) return true;
            CeilingMode mode;
            if (shape.Modifier.m_Mode == ModifierValueMode.InverseRelative) mode = CeilingMode.InverseRelative;
            else if (shape.Modifier.m_Mode == ModifierValueMode.Relative) mode = CeilingMode.Relative;
            else return false;
            if (!CeilingMath.TryExtend(shape.Slider.m_Range.min, shape.Slider.m_Range.max,
                shape.Modifier.m_Range.min, shape.Modifier.m_Range.max, multiplier, mode,
                out float sliderMax, out float modifierMax, out _)) return false;
            var slider = shape.Slider;
            var modifier = shape.Modifier;
            slider.m_Range = new Bounds1(slider.m_Range.min, sliderMax);
            modifier.m_Range = new Bounds1(modifier.m_Range.min, modifierMax);
            shape = new PolicyShape(slider, modifier);
            return true;
        }

        private bool Conflict(string message) { m_Status = "Conflict"; Mod.s_Log.Warn("[线路冲突] " + message); return false; }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Near(RouteModifier a, RouteModifier b) =>
            math.all(math.abs(a.m_Delta - b.m_Delta) <= 0.00001f * math.max(1f, math.abs(b.m_Delta)));

        private static void ValidateShape(PolicyShape shape)
        {
            if (!IsFinite(shape.Slider.m_Range.min) || !IsFinite(shape.Slider.m_Range.max) ||
                shape.Slider.m_Range.max <= shape.Slider.m_Range.min ||
                !IsFinite(shape.Modifier.m_Range.min) || !IsFinite(shape.Modifier.m_Range.max))
                throw new InvalidOperationException("Invalid vehicle policy ranges");
        }

        private static int FindInterval(DynamicBuffer<RouteModifierData> data)
        {
            int found = -1;
            for (int i = 0; i < data.Length; i++) if (data[i].m_Type == RouteModifierType.VehicleInterval)
            {
                if (found >= 0) throw new InvalidOperationException("Duplicate VehicleInterval policy entries");
                found = i;
            }
            if (found < 0) throw new InvalidOperationException("VehicleInterval policy entry missing");
            return found;
        }
    }
}
