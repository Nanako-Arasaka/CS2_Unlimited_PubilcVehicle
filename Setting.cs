using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace VehicleCapUnlock
{
    [FileLocation(nameof(VehicleCapUnlock))]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        private bool m_EnableUnlock = true;
        private int m_VehicleLimitMultiplier = 5;
        private bool m_ScaleDepotCapacity = true;
        private int m_DepotMultiplier = 5;

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSection)]
        public bool EnableUnlock
        {
            get => m_EnableUnlock;
            set
            {
                if (m_EnableUnlock == value) return;
                m_EnableUnlock = value;
                Systems.LineCeilingSystem.RequestReapply();
                Systems.DepotCapacitySystem.RequestReapply();
            }
        }

        [SettingsUISlider(min = 1, max = 10, step = 1, scalarMultiplier = 1, unit = "x")]
        [SettingsUISection(kSection)]
        public int VehicleLimitMultiplier
        {
            get => m_VehicleLimitMultiplier;
            set
            {
                if (m_VehicleLimitMultiplier == value) return;
                m_VehicleLimitMultiplier = value;
                Systems.LineCeilingSystem.RequestReapply();
            }
        }

        [SettingsUISection(kSection)]
        public bool ScaleDepotCapacity
        {
            get => m_ScaleDepotCapacity;
            set
            {
                if (m_ScaleDepotCapacity == value) return;
                m_ScaleDepotCapacity = value;
                Systems.DepotCapacitySystem.RequestReapply();
            }
        }

        [SettingsUISlider(min = 1, max = 10, step = 1, scalarMultiplier = 1, unit = "x")]
        [SettingsUISection(kSection)]
        public int DepotMultiplier
        {
            get => m_DepotMultiplier;
            set
            {
                if (m_DepotMultiplier == value) return;
                m_DepotMultiplier = value;
                Systems.DepotCapacitySystem.RequestReapply();
            }
        }

        public override void SetDefaults()
        {
            EnableUnlock = true;
            VehicleLimitMultiplier = 5;
            ScaleDepotCapacity = true;
            DepotMultiplier = 5;
        }
    }

    internal static class LocaleKeys
    {
        public const string ModName = "VehicleCapUnlock.NAME";
        public const string Section = "VehicleCapUnlock.SECTION";
        public const string EnableUnlock = "VehicleCapUnlock.OPTION.EnableUnlock";
        public const string EnableUnlockDesc = "VehicleCapUnlock.OPTION.EnableUnlock.DESC";
        public const string Multiplier = "VehicleCapUnlock.OPTION.Multiplier";
        public const string MultiplierDesc = "VehicleCapUnlock.OPTION.Multiplier.DESC";
        public const string ScaleDepot = "VehicleCapUnlock.OPTION.ScaleDepot";
        public const string ScaleDepotDesc = "VehicleCapUnlock.OPTION.ScaleDepot.DESC";
        public const string DepotMultiplier = "VehicleCapUnlock.OPTION.DepotMultiplier";
        public const string DepotMultiplierDesc = "VehicleCapUnlock.OPTION.DepotMultiplier.DESC";
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Vehicle Cap Unlock" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableUnlock)), "Unlock vehicle count ceiling" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableUnlock)),
                  "Raises the per-line 'Assigned Vehicles' ceiling. Each line is still set individually, exactly like vanilla." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.VehicleLimitMultiplier)), "Line vehicle ceiling" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.VehicleLimitMultiplier)),
                  "How far the per-line slider can be pushed, relative to the vanilla maximum. 1x = vanilla untouched." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ScaleDepotCapacity)), "Scale depot capacity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ScaleDepotCapacity)),
                  "Depots must be able to hold the extra vehicles, otherwise they never spawn. Turn off only if another mod already does this." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DepotMultiplier)), "Depot capacity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DepotMultiplier)),
                  "Multiplier applied to bus / tram / subway / train / ship / ferry / airplane depot vehicle capacity." },
            };
        }

        public void Unload() { }
    }

    public class LocaleZH : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleZH(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "出车数量上限解锁" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "主要" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableUnlock)), "解锁出车数量上限" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableUnlock)),
                  "抬高每条线路「配车数量」滑块的上限。调节方式与原版完全一致，仍然是逐条线路单独设置。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.VehicleLimitMultiplier)), "线路出车上限倍率" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.VehicleLimitMultiplier)),
                  "滑块能拉到原版上限的多少倍。1 倍 = 完全不改动原版。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ScaleDepotCapacity)), "同步放大车库容量" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ScaleDepotCapacity)),
                  "车库装不下就不会出车。只在已有其他模组负责容量时才需要关掉。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DepotMultiplier)), "车库容量倍率" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DepotMultiplier)),
                  "作用于公交 / 有轨 / 地铁 / 火车 / 船 / 轮渡 / 飞机车库的载具容量。" },
            };
        }

        public void Unload() { }
    }
}
