using HarmonyLib;
using SaveModProfiles.Data;
using SaveModProfiles.Patches;
using SaveModProfiles.UI;

namespace SaveModProfiles
{
    public sealed class ModEntry : IMod
    {
        private const string ModId = "save_mod_profiles";
        private Harmony _harmony;
        private ModLog _log;

        public void Init(ModManifest manifest)
        {
            _log = new ModLog(manifest);
            ProfileStore.Load();
        }

        public void OnEnable()
        {
            ProfileStore.ReconcileGlobalList();
            ProfileStore.CaptureStartupEnabledIds();
            _harmony = new Harmony("mod." + ModId);
            _harmony.PatchAll(typeof(ModEntry).Assembly);
            _log.Log("存档模组管理器已启用；存档清单、预设及官方模组菜单提醒已接入。");
        }

        public void OnDisable()
        {
            _harmony?.UnpatchAll("mod." + ModId);
            ProfileWindow.Close();
            _log.Log("存档模组管理器已停用。");
        }
    }
}
