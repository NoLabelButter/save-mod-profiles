using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaveModProfiles.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveModProfiles.UI
{
    internal static class RestartNoticeWindow
    {
        private const string PromptId = "save_mod_profiles_restart_prompt";
        private const string DetailsId = "save_mod_profiles_restart_details";
        private const string Accent = "#FFF09B";
        private const string Added = "#75D375";
        private const string Removed = "#F06975";
        private const string Current = "#5DB8FF";
        private const string Target = "#B889E8";
        private const string Muted = "#AAA7A8";
        private const float DetailFontSize = 21f;
        private static CustomUIWindow _window;
        private static string _message;
        private static SaveProfileChangeSnapshot _change;
        private static bool _showRestartActions;

        internal static void Show(string message, SaveProfileChangeSnapshot change)
        {
            _message = message;
            _change = change;
            _showRestartActions = true;
            BuildPrompt();
        }

        internal static void ShowStoredDetails(SaveProfileChangeRecord record)
        {
            if (record == null)
                return;
            _change = record.ToSnapshot();
            _showRestartActions = false;
            ShowDetails();
        }

        private static void BuildPrompt()
        {
            CloseWindow();
            if (GenericConfirmationUIManager.Instance != null)
            {
                GenericConfirmationUIManager.Instance.OpenPanel(
                    "<color=" + Removed + ">" + Escape(_message) + "</color>",
                    "查看本次更改",
                    "稍后重启",
                    ShowDetailsFromPrompt,
                    DismissPrompt);
                return;
            }

            if (CustomUIManager.Instance == null)
            {
                Debug.LogWarning("[SaveModProfiles] 消息窗口不可用；未记录提示内容。");
                return;
            }

            CustomUIBuilder builder = CustomUIManager.Window(PromptId, "提示", "overlay")
                .UseMetaPrefabs()
                .SetSize(760f, 230f)
                .SetCloseOnEscape();
            builder.AddWarning(_message);
            builder.AddSeparator(null, 6);
            builder.BeginRow(8f, TextAnchor.MiddleCenter);
            builder.AddButton("查看本次更改", ShowDetails, "change_details");
            builder.AddButton("稍后重启", DismissPrompt, "restart_later");
            builder.End();
            _window = builder.Center().Show();
        }

        private static void ShowDetailsFromPrompt()
        {
            GenericConfirmationUIManager.Instance.ClosePanel();
            ShowDetails();
        }

        private static void DismissPrompt()
        {
            if (GenericConfirmationUIManager.Instance != null)
                GenericConfirmationUIManager.Instance.ClosePanel();
            CloseWindow();
        }

        private static void ShowDetails()
        {
            CloseWindow();
            CustomUIBuilder builder = CustomUIManager.Window(DetailsId, "本次模组清单更改", "overlay")
                .UseMetaPrefabs()
                .SetSize(960f, 760f)
                .SetCloseOnEscape();
            builder.AddHeader("本次更改概览");
            builder.BeginScroll(550f);
            if (!string.IsNullOrEmpty(_change.recordedAt))
                builder.AddLabel("记录时间：" + _change.recordedAt);
            AddRoutePanel(builder, _change);

            builder.AddHeader("相对当前启动清单的变化");
            builder.BeginRow(12f, TextAnchor.UpperLeft);
            AddModPanel(builder, "新增", Diff(_change.targetIds, _change.startupIds), Added, "added_mods");
            AddModPanel(builder, "移除", Diff(_change.startupIds, _change.targetIds), Removed, "removed_mods");
            builder.End();

            if (_change.previousPendingIds != null)
            {
                builder.AddWarning("你之前已选择另一份待应用清单；本次选择会替换它。");
                builder.AddHeader("相对上一份待应用清单的变化");
                builder.BeginRow(12f, TextAnchor.UpperLeft);
                AddModPanel(builder, "新增", Diff(_change.targetIds, _change.previousPendingIds), Added, "pending_added_mods");
                AddModPanel(builder, "移除", Diff(_change.previousPendingIds, _change.targetIds), Removed, "pending_removed_mods");
                builder.End();
            }

            builder.AddHeader("完整清单");
            AddModPanel(builder, "游戏启动时启用", _change.startupIds, Current, "startup_mods");
            AddModPanel(builder, "目标存档使用", _change.targetIds, Target, "target_mods");
            if (_change.unavailableIds.Count > 0)
                AddModPanel(builder, "当前全局未启用", _change.unavailableIds, Muted, "unavailable_mods");
            builder.End();
            builder.AddSeparator(null, 6);
            builder.BeginRow(8f, TextAnchor.MiddleCenter);
            if (_showRestartActions)
            {
                builder.AddButton("返回提醒", BuildPrompt, "back_to_prompt");
                builder.AddButton("退出游戏", ProfileWindow.ExitGame, "exit_game");
            }
            else
                builder.AddButton("返回切换记录", ReturnToHistory, "back_to_history");
            builder.End();
            _window = builder.Center().Show();
        }

        private static void ReturnToHistory()
        {
            CloseWindow();
            ProfileWindow.ShowHistory();
        }

        private static void AddRoutePanel(CustomUIBuilder builder, SaveProfileChangeSnapshot change)
        {
            string sourceName = string.IsNullOrEmpty(change.sourceName)
                ? (change.sourceSlotId < 0 ? "当前启动清单" : SaveName(change.sourceSlotId))
                : change.sourceName;
            string targetName = string.IsNullOrEmpty(change.targetName) ? SaveName(change.targetSlotId) : change.targetName;
            builder.BeginPanel(spacing: 4f, padding: 12f, id: "save_route");
            AddRichLabel(builder, "<color=" + Current + ">" + Escape(sourceName) + "</color>  <color=" + Accent + ">→</color>  <color=" + Target + ">" + Escape(targetName) + "</color>", 26f, 42f, "save_route_text");
            if (change.previousPendingIds != null)
            {
                string previousName = !string.IsNullOrEmpty(change.previousPendingName)
                    ? change.previousPendingName
                    : (change.previousPendingSlotId < 0 ? "上一份待应用清单" : SaveName(change.previousPendingSlotId));
                AddRichLabel(builder, "待应用选择将从「" + Escape(previousName) + "」切换为「" + Escape(targetName) + "」。", 19f, 32f, "pending_route_text");
            }
            builder.End();
        }

        private static void AddModPanel(CustomUIBuilder builder, string title, List<string> ids, string color, string id)
        {
            builder.BeginPanel(spacing: 3f, padding: 10f, id: id + "_panel");
            builder.AddHeader("<color=" + color + ">" + Escape(title) + "  ·  " + ids.Count + "</color>", id + "_header");
            StringBuilder text = new StringBuilder();
            AppendModList(text, ids, color);
            AddRichLabel(builder, text.ToString().TrimEnd(), DetailFontSize, EstimateHeight(ids), id + "_list");
            builder.End();
        }

        private static void AddRichLabel(CustomUIBuilder builder, string value, float fontSize, float preferredHeight, string id)
        {
            builder.AddLabel(value, id);
            CustomUIElement element = builder.Last;
            if (element == null || element.text == null)
                return;
            element.text.richText = true;
            element.text.fontSize = fontSize;
            element.text.enableWordWrapping = true;
            element.text.alignment = TextAlignmentOptions.TopLeft;
            element.text.overflowMode = TextOverflowModes.Overflow;
            LayoutElement layout = element.root.GetComponent<LayoutElement>();
            if (layout == null)
                layout = element.root.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = preferredHeight;
        }

        private static void AppendModList(StringBuilder text, IEnumerable<string> ids, string color)
        {
            bool any = false;
            foreach (string id in ids)
            {
                any = true;
                text.Append("• <color=").Append(color).Append(">")
                    .Append(Escape(ModLabel(id))).Append("</color> <color=").Append(Muted).Append(">· ")
                    .Append(Escape(id)).Append("</color>\n");
            }
            if (!any)
                text.Append("• <color=").Append(Muted).Append(">没有模组变化</color>\n");
        }

        private static float EstimateHeight(ICollection<string> ids) => Mathf.Max(34f, ids.Count * 32f);

        private static List<string> Diff(IEnumerable<string> left, IEnumerable<string> right)
        {
            HashSet<string> rightSet = new HashSet<string>(right);
            return left.Where(id => !rightSet.Contains(id)).ToList();
        }

        private static string ModLabel(string id)
        {
            LoadedMod mod = ModLoader.Mods.FirstOrDefault(item => item.Manifest != null && item.Manifest.ID == id);
            return mod == null ? id : ModLoader.GetModLabel(mod.Manifest);
        }

        private static string SaveName(int slotId)
        {
            SavePreviewData preview = SaveFiles.IndexLoad<SavePreviewData>("preview_" + slotId, null);
            return preview == null ? "存档 " + slotId : preview.GetDisplayName();
        }

        private static string Escape(string value) => (value ?? "")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        private static void CloseWindow()
        {
            if (_window != null && _window.IsAlive)
                _window.Close();
            _window = null;
        }
    }
}
