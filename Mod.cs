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
            Systems.LineCeilingSystem.ResumeAll();
            Systems.DepotCapacitySystem.ResumeAll();

            s_Log.Info("VehicleCapUnlock: systems registered");
        }

        public void OnDispose()
        {
            s_Log.Info("VehicleCapUnlock: OnDispose");
            // Restore synchronously while the ECS World is still available.
            bool linesRestored = Systems.LineCeilingSystem.RestoreAllNow();
            bool depotsRestored = Systems.DepotCapacitySystem.RestoreAllNow();
            if (!linesRestored || !depotsRestored)
                s_Log.Warn($"[还原未完成] lines={linesRestored}; depots={depotsRestored}; 不应把本次退出视为完成卸载清理");
            s_Setting?.UnregisterInOptionsUI();
            s_Setting = null;
        }
    }
}
