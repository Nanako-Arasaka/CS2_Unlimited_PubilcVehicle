using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace VehicleCapUnlock
{
    public class Mod : IMod
    {
        public static readonly ILog s_Log =
            LogManager.GetLogger(nameof(VehicleCapUnlock)).SetShowsErrorsInUI(true);

        public static Setting s_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info("VehicleCapUnlock: OnLoad");

            s_Setting = new Setting(this);
            s_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(s_Setting));
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleZH(s_Setting));
            AssetDatabase.global.LoadSettings(nameof(VehicleCapUnlock), s_Setting, new Setting(this));

            // 两个系统都挂在 GameSimulation，只在城市加载完成 / 设置变更时干一次活，之后空转。
            updateSystem.UpdateAt<Systems.LineCeilingSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.DepotCapacitySystem>(SystemUpdatePhase.GameSimulation);

            s_Log.Info("VehicleCapUnlock: systems registered");
        }

        public void OnDispose()
        {
            s_Log.Info("VehicleCapUnlock: OnDispose");
            // 退出时尽量把 prefab 还原成原版，避免残留污染存档。
            Systems.LineCeilingSystem.RequestRestore();
            Systems.DepotCapacitySystem.RequestRestore();
            s_Setting?.UnregisterInOptionsUI();
            s_Setting = null;
        }
    }
}
