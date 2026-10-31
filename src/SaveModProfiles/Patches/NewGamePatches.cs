using HarmonyLib;
using SaveModProfiles.Data;
using SaveModProfiles.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveModProfiles.Patches
{
    [HarmonyPatch(typeof(NewGameSpecializationUI), "Open")]
    internal static class NewGameProfileButtonPatch
    {
        private static void Postfix()
        {
            NewGameSpecializationFrame frame = AccessTools.Field(typeof(NewGameSpecializationUI), "frame").GetValue(null) as NewGameSpecializationFrame;
            if (frame == null || frame.continueButton == null)
                return;

            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
                return;

            RectTransform continueRect = frame.continueButton.GetComponent<RectTransform>();
            RectTransform buttonParent = continueRect.parent as RectTransform;
            if (buttonParent != null && buttonParent.Find("save_mod_profiles_new_game") == null)
            {
                float x = continueRect.anchoredPosition.x - 40f;
                float y = -continueRect.anchoredPosition.y + (continueRect.rect.height - 32f) * 0.5f;
                NewGameUIElements.NewIconButton(buttonParent, "save_mod_profiles_new_game", x, y, 32f,
                    manager.spriteSetting, manager.spriteSettingHL, "选择新游戏使用的模组", ProfileWindow.OpenForNewGame);
            }

            AddSaveNameField(frame);
        }

        private static void AddSaveNameField(NewGameSpecializationFrame frame)
        {
            RectTransform title = frame.headerTitle.rectTransform;
            RectTransform parent = title.parent as RectTransform;
            if (parent == null || parent.Find("save_mod_profiles_save_name") != null)
                return;

            Vector3[] titleCorners = new Vector3[4];
            title.GetWorldCorners(titleCorners);
            Vector3 titleTopLeft = parent.InverseTransformPoint(titleCorners[1]);
            Vector3 titleBottomLeft = parent.InverseTransformPoint(titleCorners[0]);
            float titleTextWidth = frame.headerTitle.GetPreferredValues(frame.headerTitle.text).x;
            float x = titleTopLeft.x - parent.rect.xMin + titleTextWidth + 20f;
            float height = 48f;
            float y = parent.rect.yMax - titleTopLeft.y + (titleTopLeft.y - titleBottomLeft.y - height) * 0.5f;
            float width = Mathf.Min(304f, parent.rect.width - x - 24f);
            if (width < 220f)
                return;

            NewGameUIElements.NewImage(parent, "save_mod_profiles_save_name_border", x, y, width, 52f,
                frame.frame2Square, NewGameStyle.BUTTON_BORDER_COLOR);
            NewGameUIElements.NewImage(parent, "save_mod_profiles_save_name_background", x + 4f, y + 4f, width - 8f, 44f,
                null, NewGameStyle.BUTTON_BG);
            TMP_InputField input = NewGameUIElements.NewInputField(frame, parent, "save_mod_profiles_save_name",
                x + 10f, y + 4f, width - 20f, 44f, FontLoader.FontRole.Noto_Sans_Normal, 24f,
                NewGameStyle.LOAD_INFO_COLOR, 40);
            TextMeshProUGUI placeholder = NewGameUIElements.NewText(frame, input.transform, "Placeholder",
                4f, 0f, width - 28f, 44f, FontLoader.FontRole.Noto_Sans_Normal, 24f,
                NewGameStyle.LOAD_INFO_COLOR, TextAlignmentOptions.MidlineLeft, wrap: false);
            placeholder.text = "存档名字";
            input.placeholder = placeholder;
            input.text = ProfileStore.PendingNewGameName;
            input.onValueChanged.AddListener(ProfileStore.SavePendingNewGameName);
        }
    }

    [HarmonyPatch(typeof(NewGameSpecializationUI), "OnContinueClicked")]
    internal static class ApplyNewGameProfileBeforePerksPatch
    {
        private static bool Prefix()
        {
            bool restart = ProfileStore.ApplyNewGameProfile(out System.Collections.Generic.List<string> excluded);
            if (!restart)
                return true;

            string note = excluded.Count == 0 ? "" : "\n全局已关闭、暂不能加载：" + string.Join("、", excluded);
            ProfileWindow.ShowMessage("新游戏模组清单已应用，但本次启动尚未加载所选模组。请先重启游戏，再从专精页继续；重启后才能显示对应的模组天赋。" + note);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "InitialSave")]
    internal static class AssignNewGameProfilePatch
    {
        private static void Postfix(PlayerStore __instance) => ProfileStore.AssignPendingNewGameProfile(__instance.saveSlotId);
    }

    [HarmonyPatch(typeof(SavePreviewData), "GenerateData")]
    internal static class PreserveCustomSaveNamePatch
    {
        private static void Postfix(ref SavePreviewData __result)
        {
            if (__result == null)
                return;
            string customName = ProfileStore.SaveNameForPreview(__result.slotId);
            if (!string.IsNullOrWhiteSpace(customName))
                __result.customName = customName;
        }
    }
}
