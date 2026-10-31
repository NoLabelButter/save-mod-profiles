using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using SaveModProfiles.Data;
using SaveModProfiles.UI;
using TMPro;
using UnityEngine;

namespace SaveModProfiles.Patches
{
    [HarmonyPatch(typeof(LoadGameUI), "Bind")]
    internal static class SaveDifficultyTitleColorPatch
    {
        private static readonly Color32 CozyColor = new Color32(117, 211, 117, 255);
        private static readonly Color32 NormalColor = new Color32(93, 184, 255, 255);
        private static readonly Color32 HardColor = new Color32(184, 137, 232, 255);
        private static readonly Color32 ChallengeColor = new Color32(240, 105, 117, 255);

        private static void Postfix(object __0)
        {
            Type plateType = __0.GetType();
            int slotId = (int)AccessTools.Field(plateType, "slotId").GetValue(__0);
            TextMeshProUGUI title = AccessTools.Field(plateType, "title").GetValue(__0) as TextMeshProUGUI;
            if (title == null || slotId < 0)
                return;

            Color titleColor = GetTitleColor(ProfileStore.DifficultyIdForSave(slotId));
            title.color = titleColor;
            WildNetworkSlot hover = title.GetComponent<WildNetworkSlot>();
            if (hover != null)
            {
                hover.onHover = (_, entering) => title.color = entering ? NewGameStyle.BUTTON_HOVER : titleColor;
            }
        }

        internal static Color GetTitleColor(string difficultyId)
        {
            switch (difficultyId)
            {
                case "cozy": return CozyColor;
                case "normal": return NormalColor;
                case "hard": return HardColor;
                case "challenge": return ChallengeColor;
                default: return NewGameStyle.LOAD_TITLE_COLOR;
            }
        }
    }

    [HarmonyPatch(typeof(LoadGameUI), "BeginRename")]
    internal static class SaveDifficultyRenameColorPatch
    {
        private static void Postfix(object __0)
        {
            Type plateType = __0.GetType();
            int slotId = (int)AccessTools.Field(plateType, "slotId").GetValue(__0);
            TMP_InputField input = AccessTools.Field(plateType, "rename").GetValue(__0) as TMP_InputField;
            if (input == null || input.textComponent == null)
                return;

            input.textComponent.color = SaveDifficultyTitleColorPatch.GetTitleColor(ProfileStore.DifficultyIdForSave(slotId));
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "SaveGame")]
    internal static class SaveDifficultyAfterSavePatch
    {
        private static void Postfix(PlayerStore __instance)
        {
            ProfileStore.SaveDifficultyForSlot(__instance.saveSlotId, __instance.difficultyId, __instance.isHardMode);
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "LoadGame")]
    internal static class SaveDifficultyAfterLoadPatch
    {
        private static void Postfix(PlayerStore __instance)
        {
            ProfileStore.SaveDifficultyForSlot(__instance.saveSlotId, __instance.difficultyId, __instance.isHardMode);
            ProfileStore.SetLastEnteredSave(__instance.saveSlotId);
        }
    }

    [HarmonyPatch(typeof(LoadGameUI), "RebuildList")]
    internal static class SaveListRefreshPatch
    {
        private static void Postfix() => ProfileStore.PrepareExistingSaveProfiles();
    }

    [HarmonyPatch(typeof(LoadGameUI), "RebuildList")]
    internal static class LastEnteredSaveSectionRefreshPatch
    {
        private static void Prefix() => LastEnteredSaveSection.Refresh();
    }

    [HarmonyPatch(typeof(LoadGameUI), "PlateTop")]
    internal static class OffsetSaveListForLastEnteredPatch
    {
        private static readonly FieldInfo ExpandedIndex = AccessTools.Field(typeof(LoadGameUI), "expandedIndex");
        private static readonly FieldInfo ExpandedExtra = AccessTools.Field(typeof(LoadGameUI), "expandedExtra");

        private static void Postfix(int index, ref float __result)
        {
            float offset = LastEnteredSaveSection.ListOffset;
            if (offset <= 0f)
                return;

            int expandedIndex = (int)ExpandedIndex.GetValue(null);
            float expandedExtra = (float)ExpandedExtra.GetValue(null);
            __result = index * 104f + (expandedIndex >= 0 && index > expandedIndex ? expandedExtra : 0f) + offset;
        }
    }

    [HarmonyPatch(typeof(LoadGameUI), "BuildPlateView")]
    internal static class SaveProfileButtonPatch
    {
        private static void Postfix(object __result)
        {
            if (__result == null || CustomUIManager.Instance == null)
                return;

            Type plateType = __result.GetType();
            RectTransform root = AccessTools.Field(plateType, "root").GetValue(__result) as RectTransform;
            if (root == null || root.Find("save_mod_profiles_button") != null)
                return;

            RectTransform loadLatest = root.Find("Body/LoadLatest") as RectTransform;
            if (loadLatest != null)
            {
                ResizeButtonPart(loadLatest, 150f);
                ResizeButtonPart(loadLatest.Find("Outer") as RectTransform, 150f);
                ResizeButtonPart(loadLatest.Find("Inner") as RectTransform, 142f);
                ResizeButtonPart(loadLatest.Find("Label") as RectTransform, 142f);
            }

            Sprite normal = CustomUIManager.Instance.spriteSetting;
            Sprite hover = CustomUIManager.Instance.spriteSettingHL;
            NewGameUIElements.NewIconButton(root, "save_mod_profiles_button", 766f, 32f, 32f, normal, hover,
                "配置此存档使用的模组", () => OpenForPlate(__result, plateType));
        }

        private static void ResizeButtonPart(RectTransform part, float width)
        {
            if (part == null)
                return;
            Vector2 size = part.sizeDelta;
            part.sizeDelta = new Vector2(width, size.y);
        }

        private static void OpenForPlate(object plate, Type plateType)
        {
            int slotId = (int)AccessTools.Field(plateType, "slotId").GetValue(plate);
            if (slotId >= 0)
                ProfileWindow.OpenForSave(slotId);
        }
    }

    [HarmonyPatch(typeof(LoadGameUI), "OnLoadClicked")]
    internal static class ApplySaveProfileBeforeLoadPatch
    {
        private static bool Prefix(int slotId, string file)
        {
            string loadFile = file ?? SaveFiles.Slot(slotId);
            if (!File.Exists(SaveFiles.AbsolutePath(loadFile)))
                return true;

            bool restart = ProfileStore.ApplySaveProfile(slotId, out List<string> excluded, out SaveProfileChangeSnapshot change);
            if (!restart)
                return true;

            SavePreviewData preview = SaveUIManager.GetPreview(slotId);
            string saveName = preview == null ? "存档 " + slotId : preview.GetDisplayName();
            string note = excluded.Count == 0 ? "" : "\n全局已关闭、暂不能加载：" + string.Join("、", excluded);
            RestartNoticeWindow.Show("存档“" + saveName + "”的模组清单已设为下次启动时使用。请重启游戏，再从存档列表重新进入。\n重启前若再选择其他存档，待应用的清单会改为最后选择的存档。" + note, change);
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveFiles), "RegisterSlot")]
    internal static class RegisterSaveProfilePatch
    {
        private static void Postfix(int slotId) => ProfileStore.GetOrCreateSave(slotId);
    }

    [HarmonyPatch(typeof(SaveFiles), "DuplicateSlot")]
    internal static class DuplicateSaveProfilePatch
    {
        private static void Postfix(int slotId, int __result)
        {
            if (__result >= 0)
                ProfileStore.CopySaveProfile(slotId, __result);
        }
    }

    [HarmonyPatch(typeof(SaveFiles), "DeleteSlot")]
    internal static class DeleteSaveProfilePatch
    {
        private static void Postfix(int slotId) => ProfileStore.DeleteSaveProfile(slotId);
    }
}
