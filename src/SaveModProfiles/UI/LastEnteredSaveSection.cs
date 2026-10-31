using System.IO;
using System.Linq;
using HarmonyLib;
using SaveModProfiles.Data;
using SaveModProfiles.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveModProfiles.UI
{
    internal static class LastEnteredSaveSection
    {
        private const float ListOffsetPixels = 192f;
        private static RectTransform _root;
        private static RectTransform _content;
        private static TextMeshProUGUI _title;
        private static TextMeshProUGUI _info;
        private static TextMeshProUGUI _date;
        private static TextMeshProUGUI _version;
        private static Image _icon;
        private static NewGameCommonButton _loadButton;
        private static bool _isVisible;

        internal static float ListOffset => _isVisible ? ListOffsetPixels : 0f;

        internal static void Refresh()
        {
            int slotId = ProfileStore.LastEnteredSlotId;
            string savePath = slotId < 0 ? "" : SaveFiles.AbsolutePath(SaveFiles.Slot(slotId));
            var slots = SaveUIManager.GetOrderedSlotIds();
            bool hasSaves = slots.Count > 0;
            bool hasSave = slotId >= 0 && slots.Contains(slotId) && File.Exists(savePath);
            LoadGameFrame frame = AccessTools.Field(typeof(LoadGameUI), "frame").GetValue(null) as LoadGameFrame;
            if (!hasSaves || frame == null || frame.content == null)
            {
                _isVisible = false;
                DestroySection();
                return;
            }

            _isVisible = true;
            if (_root == null || _content != frame.content)
                Build(frame);
            SavePreviewData data = hasSave ? SaveUIManager.GetPreview(slotId) : null;
            if (data == null)
                ShowEmptyState();
            else
                UpdateCard(slotId, data);
        }

        private static void Build(LoadGameFrame frame)
        {
            DestroySection();
            _content = frame.content;
            _root = NewGameUIElements.NewRect(_content, "save_mod_profiles_last_entered", 0f, 0f, 900f, 164f);
            NewGameUIElements.NewImage(_root, "SectionBorder", 0f, 0f, 900f, 164f,
                frame.frame2Square, NewGameStyle.BUTTON_HOVER);
            NewGameUIElements.NewText(frame, _root, "Header", 8f, 4f, 560f, 36f,
                FontLoader.FontRole.Lubrifont, 28f, NewGameStyle.BUTTON_HOVER,
                TextAlignmentOptions.MidlineLeft, wrap: false).text = "上次运行的存档";

            RectTransform card = NewGameUIElements.NewRect(_root, "Card", 8f, 44f, 884f, 96f);
            NewGameUIElements.NewImage(card, "Border", 0f, 0f, 884f, 96f,
                frame.frame2Square, NewGameStyle.LOAD_PLATE_BORDER_COLOR);
            NewGameUIElements.NewImage(card, "IconPlate", 2f, 2f, 64f, 92f,
                null, NewGameStyle.LOAD_ICON_PLATE_COLOR);
            NewGameUIElements.NewImage(card, "Ring", 12f, 27f, 42f, 42f,
                frame.iconRing, Color.white);
            _icon = NewGameUIElements.NewImage(card, "Icon", 12f, 27f, 38f, 38f, null, Color.white);
            _title = NewGameUIElements.NewText(frame, card, "Title", 82f, 13f, 308f, 38f,
                FontLoader.FontRole.Lubrifont, 32f, NewGameStyle.LOAD_TITLE_COLOR,
                TextAlignmentOptions.MidlineLeft, wrap: false);
            WildNetworkUI.AutoSize(_title, 18f);
            _info = NewGameUIElements.NewText(frame, card, "Info", 82f, 56f, 308f, 27f,
                FontLoader.FontRole.Noto_Sans_Normal, 20f, NewGameStyle.LOAD_INFO_COLOR,
                TextAlignmentOptions.MidlineLeft, wrap: false);
            WildNetworkUI.AutoSize(_info, 12f);
            _date = NewGameUIElements.NewText(frame, card, "Date", 310f, 21f, 260f, 25f,
                FontLoader.FontRole.Noto_Sans_Normal, 18f, NewGameStyle.LOAD_DATE_COLOR,
                TextAlignmentOptions.MidlineRight, wrap: false);
            _version = NewGameUIElements.NewText(frame, card, "Version", 310f, 50f, 260f, 25f,
                FontLoader.FontRole.Noto_Sans_Normal, 18f, NewGameStyle.LOAD_VERSION_COLOR,
                TextAlignmentOptions.MidlineRight, wrap: false);
            WildNetworkUI.AutoSize(_version, 12f);
            NewGameUIElements.NewImage(card, "Separator", 586f, 16f, 2f, 64f, null, NewGameStyle.SEPARATOR);
            _loadButton = NewGameCommonButton.Build(frame, card, "LoadLatest", 604f, 27f, 192f, 42f,
                LocHelper.GetLocalizedOrDefault("MainMenu", "load_latest", "Load Latest"), LoadLastEntered);
        }

        private static void ShowEmptyState()
        {
            _title.text = "尚未记录进入过的存档";
            _title.color = NewGameStyle.LOAD_TITLE_COLOR;
            _info.text = "载入任一存档后，这里会显示快捷入口";
            _date.text = "";
            _version.text = "";
            _icon.enabled = false;
            _loadButton.SetInteractable(false);
            _loadButton.label.text = "尚无记录";
        }

        private static void UpdateCard(int slotId, SavePreviewData data)
        {
            _title.text = data.GetDisplayName();
            _title.color = SaveDifficultyTitleColorPatch.GetTitleColor(ProfileStore.DifficultyIdForSave(slotId));
            _info.text = LocHelper.GetLocalizedMechanic("mech_day_label", data.dayCount)
                + " | " + LocHelper.GetLocalizedUI("network_ui_cost", data.credit);
            _date.text = data.lastSaveDateTime.ToString("dd/MM/yyyy - HH:mm:ss");
            _version.text = LocHelper.GetLocalizedOrDefault("MainMenu", "load_version", "Version: {0}", data.version);
            _icon.sprite = IconProvider.Instance == null ? null : IconProvider.Instance.GetIcon(data.startType, data.startScenarioId);
            _icon.enabled = _icon.sprite != null;
            if (_icon.sprite != null)
                NewGameUIElements.FitPixelArt(_icon, 12f, 27f, 42f, 38f);
            _loadButton.SetInteractable(true);
            _loadButton.label.text = LocHelper.GetLocalizedOrDefault("MainMenu", "load_latest", "Load Latest");
        }

        private static void LoadLastEntered()
        {
            int slotId = ProfileStore.LastEnteredSlotId;
            if (slotId >= 0)
                AccessTools.Method(typeof(LoadGameUI), "OnLoadClicked").Invoke(null, new object[] { slotId, null, null });
        }

        private static void DestroySection()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
            _root = null;
            _content = null;
            _title = null;
            _info = null;
            _date = null;
            _version = null;
            _icon = null;
            _loadButton = null;
        }
    }
}
