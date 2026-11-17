using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace VehicleCapUnlock.Systems
{
    public partial class DepotCapacitySystem : GameSystemBase
    {
        private sealed class Snapshot
        {
            public PrefabBase Prefab;
            public int Capacity;
        }

        private static readonly HashSet<DepotCapacitySystem> s_Instances = new HashSet<DepotCapacitySystem>();
        private readonly Dictionary<Entity, Snapshot> m_Originals = new Dictionary<Entity, Snapshot>();
        private bool m_PendingApply = true;
        private bool m_Active = true;
        private EntityQuery m_DepotQuery;
        private PrefabSystem m_PrefabSystem;

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
            m_DepotQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(),
                ComponentType.ReadWrite<TransportDepotData>());
            s_Instances.Add(this);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game) m_PendingApply = true;
            // Do not discard baselines just because another city was loaded.
        }

        protected override void OnUpdate()
        {
            if (!m_Active || !m_PendingApply) return;
            m_PendingApply = false;
            try { m_PendingApply = !Apply(); }
            catch (Exception e) { Mod.s_Log.Error(e, "DepotCapacitySystem.Apply 失败"); }
        }

        protected override void OnDestroy()
        {
            RestoreNow();
            m_Originals.Clear();
            s_Instances.Remove(this);
            base.OnDestroy();
        }

        private bool Apply()
        {
            if (m_DepotQuery.IsEmptyIgnoreFilter) return false;
            var setting = Mod.s_Setting;
            int multiplier = setting == null ? 1 : Math.Max(1, Math.Min(10, setting.DepotMultiplier));
            if (setting == null || !setting.EnableUnlock || !setting.ScaleDepotCapacity || multiplier == 1)
            {
                RestoreNow();
                return true;
            }

            EntityManager.CompleteAllTrackedJobs();
            int changed = 0;
            using (var entities = m_DepotQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in entities)
                {
                    var depot = EntityManager.GetComponentData<TransportDepotData>(entity);
                    if (!IsPassengerDepot(depot.m_TransportType)) continue;
                    var prefab = m_PrefabSystem.GetPrefab<PrefabBase>(entity);
                    if (!m_Originals.TryGetValue(entity, out var snapshot) ||
                        !ReferenceEquals(snapshot.Prefab, prefab))
                    {
                        snapshot = new Snapshot { Prefab = prefab, Capacity = depot.m_VehicleCapacity };
                        m_Originals[entity] = snapshot;
                    }

                    int target = (int)Math.Max(1L, Math.Min(int.MaxValue, (long)snapshot.Capacity * multiplier));
                    if (depot.m_VehicleCapacity == target) continue;
                    depot.m_VehicleCapacity = target;
                    EntityManager.SetComponentData(entity, depot);
                    changed++;
                }
            }
            Mod.s_Log.Info($"[车库] 初始容量 x{multiplier}，改动 {changed} 个 prefab");
            return true;
        }

        private void RestoreNow()
        {
            if (m_Originals.Count == 0) return;
            try
            {
                if (!World.IsCreated) return;
                EntityManager.CompleteAllTrackedJobs();
                int restored = 0;
                foreach (var pair in m_Originals)
                {
                    Entity entity = pair.Key;
                    if (!EntityManager.Exists(entity) ||
                        !EntityManager.HasComponent<TransportDepotData>(entity) ||
                        !ReferenceEquals(m_PrefabSystem.GetPrefab<PrefabBase>(entity), pair.Value.Prefab)) continue;
                    var depot = EntityManager.GetComponentData<TransportDepotData>(entity);
                    if (depot.m_VehicleCapacity == pair.Value.Capacity) continue;
                    depot.m_VehicleCapacity = pair.Value.Capacity;
                    EntityManager.SetComponentData(entity, depot);
                    restored++;
                }
                if (restored > 0) Mod.s_Log.Info($"[车库] 已同步还原 {restored} 个 prefab");
            }
            catch (Exception e) { Mod.s_Log.Error(e, "车库容量同步还原失败"); }
        }

        private static bool IsPassengerDepot(TransportType type)
        {
            switch (type)
            {
                case TransportType.Bus:
                case TransportType.Tram:
                case TransportType.Subway:
                case TransportType.Train:
                case TransportType.Ship:
                case TransportType.Ferry:
                case TransportType.Airplane:
                    return true;
                default:
                    return false;
            }
        }
    }
}
