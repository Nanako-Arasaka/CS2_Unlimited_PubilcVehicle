using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace VehicleCapUnlock.Systems
{
    /// <summary>
    /// 放大各类交通车库的载具容量。
    ///
    /// 车库容量是「物理出车上限」——滑块拉得再高，车库装不下就不会出车。
    /// 对应字段：TransportDepotData.m_VehicleCapacity（prefab 上的 ECS 组件，模拟层直接读它）。
    /// 原版基准值在另一个托管组件 TransportDepot 上，这里改成「首次运行时抓取快照」，
    /// 保证反复应用不会叠乘（这也是 All Transit + Trucks 的做法）。
    /// </summary>
    public partial class DepotCapacitySystem : GameSystemBase
    {
        private static readonly Dictionary<Entity, int> s_Originals = new Dictionary<Entity, int>();
        private static bool s_PendingApply = true;
        private static bool s_PendingRestore;

        private EntityQuery m_DepotQuery;

        public static void RequestReapply() { s_PendingApply = true; }
        public static void RequestRestore() { s_PendingRestore = true; }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_DepotQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PrefabData>(),
                    ComponentType.ReadWrite<TransportDepotData>(),
                }
            });
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game && (purpose == Purpose.NewGame || purpose == Purpose.LoadGame))
            {
                s_Originals.Clear();
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
                Mod.s_Log.Error(e, "DepotCapacitySystem.Apply 失败");
            }
        }

        private void Apply(bool restore)
        {
            if (m_DepotQuery.IsEmptyIgnoreFilter) return;

            Setting s = Mod.s_Setting;
            bool enable = s == null || (s.EnableUnlock && s.ScaleDepotCapacity);
            float k = s == null ? 1f : Math.Max(1, Math.Min(10, s.DepotMultiplier));

            if (restore || !enable || k <= 1f)
            {
                RestoreAll();
                return;
            }

            NativeArray<Entity> entities = m_DepotQuery.ToEntityArray(Allocator.Temp);
            NativeArray<TransportDepotData> depots = m_DepotQuery.ToComponentDataArray<TransportDepotData>(Allocator.Temp);

            int changed = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity e = entities[i];
                TransportDepotData d = depots[i];

                // 只放大客运车库；Post / Helicopter / Work / Bicycle / Car 等不动。
                if (!IsPassengerDepot(d.m_TransportType)) continue;

                if (!s_Originals.TryGetValue(e, out int original))
                {
                    original = d.m_VehicleCapacity;
                    s_Originals[e] = original;
                }

                int target = (int)Math.Round(original * (double)k);
                if (target < 1) target = 1;

                if (d.m_VehicleCapacity != target)
                {
                    d.m_VehicleCapacity = target;
                    depots[i] = d;
                    changed++;
                }
            }

            if (changed > 0)
            {
                m_DepotQuery.CopyFromComponentDataArray(depots);
                Mod.s_Log.Info($"[车库] 缩放 x{k}，改动 {changed} 个车库 prefab（共 {entities.Length}）");
            }

            entities.Dispose();
            depots.Dispose();
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
                case TransportType.Taxi:
                case TransportType.Airplane:
                    return true;
                default:
                    return false;
            }
        }

        private void RestoreAll()
        {
            if (s_Originals.Count == 0) return;

            NativeArray<Entity> entities = m_DepotQuery.ToEntityArray(Allocator.Temp);
            NativeArray<TransportDepotData> depots = m_DepotQuery.ToComponentDataArray<TransportDepotData>(Allocator.Temp);

            int restored = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (!s_Originals.TryGetValue(entities[i], out int original)) continue;

                TransportDepotData d = depots[i];
                if (d.m_VehicleCapacity != original)
                {
                    d.m_VehicleCapacity = original;
                    depots[i] = d;
                    restored++;
                }
            }

            if (restored > 0)
            {
                m_DepotQuery.CopyFromComponentDataArray(depots);
                Mod.s_Log.Info($"[车库] 还原 {restored} 个车库 prefab 为原版容量");
            }

            entities.Dispose();
            depots.Dispose();
        }
    }
}
