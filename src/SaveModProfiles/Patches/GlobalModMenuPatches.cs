using HarmonyLib;
using SaveModProfiles.UI;

namespace SaveModProfiles.Patches
{
    [HarmonyPatch(typeof(ModUIManager), "ToggleSelectedMod")]
    internal static class WarnBeforeGlobalDisablePatch
    {
        private static bool Prefix(ModUIManager __instance, string ___selectedModID)
        {
            if (string.IsNullOrEmpty(___selectedModID) || !ModLoader.IsModEnabled(___selectedModID))
                return true;

            GlobalDisableWindow.Open(__instance, ___selectedModID);
            return false;
        }
    }

    [HarmonyPatch(typeof(ConfirmPanelUIManager), "OnYesClick")]
    internal static class RestoreProfileWindowAfterConfirmYesPatch
    {
        private static void Postfix() => ProfileWindow.RestoreAfterOfficialConfirmationClosed();
    }

    [HarmonyPatch(typeof(ConfirmPanelUIManager), "OnNoClick")]
    internal static class RestoreProfileWindowAfterConfirmNoPatch
    {
        private static void Postfix() => ProfileWindow.RestoreAfterOfficialConfirmationClosed();
    }
}
