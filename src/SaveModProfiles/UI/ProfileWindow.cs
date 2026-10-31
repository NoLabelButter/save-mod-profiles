using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SaveModProfiles.Data;
using UnityEngine;

namespace SaveModProfiles.UI
{
    internal static class ProfileWindow
    {
        private const string WindowId = "save_mod_profiles_editor";
        private const string MessageWindowId = "save_mod_profiles_message";
        private static readonly MethodInfo BeginEnableMethod = typeof(ModUIManager).GetMethod(
            "BeginEnable", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(LoadedMod), typeof(Action) }, null);
        private static CustomUIWindow _window;
        private static CustomUIWindow _messageWindow;
        private static int _slotId = -1;
        private static bool _isNewGame;
        private static HashSet<string> _draft = new HashSet<string>();
        private static string _statusMessage;
        private static bool _statusNeedsRestart;
        private static bool _showPresetTab;
        private static bool _showHistoryTab;
        private static bool _showInactiveMods;
        private static int _modSortMode;
        private static string _modSearchDraft = string.Empty;
        private static string _modSearchText = string.Empty;
        private static readonly List<ModListRow> _modListRows = new List<ModListRow>();
        private static readonly Dictionary<int, GameObject> _modGroupRows = new Dictionary<int, GameObject>();
        private static CustomUIElement _modSearchEmptyMessage;
        private static bool _awaitingOfficialConfirmation;
        private static bool _pendingApprovalRefresh;
        private static CustomUIWindow _gameWindowHiddenForOfficialConfirmation;

        internal static void OpenForSave(int slotId)
        {
            ProfileStore.ReconcileGlobalList();
            _slotId = slotId;
            _isNewGame = false;
            _draft = new HashSet<string>(ProfileStore.GetOrCreateSave(slotId).modIds);
            PresetManagement.Reset();
            _statusMessage = null;
            _statusNeedsRestart = false;
            _showPresetTab = false;
            _showHistoryTab = false;
            _modSearchDraft = string.Empty;
            _modSearchText = string.Empty;
            Build();
        }

        internal static void OpenForNewGame()
        {
            ProfileStore.ReconcileGlobalList();
            _slotId = -1;
            _isNewGame = true;
            _draft = new HashSet<string>(ProfileStore.PendingNewGameIds);
            PresetManagement.Reset();
            _statusMessage = null;
            _statusNeedsRestart = false;
            _showPresetTab = false;
            _showHistoryTab = false;
            _modSearchDraft = string.Empty;
            _modSearchText = string.Empty;
            Build();
        }

        internal static void Close()
        {
            if (_window != null && _window.IsAlive)
                _window.Close();
            _window = null;
            _modListRows.Clear();
            _modGroupRows.Clear();
            _modSearchEmptyMessage = null;
        }

        private static void Build()
        {
            Close();
            if (CustomUIManager.Instance == null)
            {
                Debug.LogError("[SaveModProfiles] CustomUIManager 尚未就绪，不能打开配置页。");
                return;
            }

            HashSet<string> globalEnabledIds = new HashSet<string>(ProfileStore.GlobalEnabledIds, StringComparer.Ordinal);
            IEnumerable<LoadedMod> filteredMods = ModLoader.Mods
                .Where(mod => mod.Manifest != null && (_showInactiveMods || globalEnabledIds.Contains(mod.Manifest.ID)));
            IOrderedEnumerable<LoadedMod> orderedMods = _modSortMode == 0
                ? filteredMods.OrderBy(mod => GetModStatusSortGroup(mod, globalEnabledIds))
                    .ThenBy(GetModLabel, StringComparer.CurrentCultureIgnoreCase)
                : _modSortMode == 2
                    ? filteredMods.OrderByDescending(GetModLabel, StringComparer.CurrentCultureIgnoreCase)
                    : filteredMods.OrderBy(GetModLabel, StringComparer.CurrentCultureIgnoreCase);
            List<LoadedMod> availableMods = orderedMods.ToList();
            float listHeight = Mathf.Clamp(availableMods.Count * 56f + (_modSortMode == 0 ? 96f : 24f), 120f, 320f);
            float windowHeight = _showPresetTab
                ? 450f + (PresetManagement.HasStatus ? 60f : 0f)
                : _showHistoryTab ? 560f
                : 365f + listHeight + (string.IsNullOrEmpty(_statusMessage) ? 0f : (_statusNeedsRestart ? 126f : 76f));

            CustomUIBuilder builder = CustomUIManager.Window(WindowId, "存档模组管理器", "overlay")
                .UseMetaPrefabs()
                .SetSize(920f, windowHeight)
                .SetCloseOnEscape();

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddButton("存档模组配置", ShowModTab, "tab_mods").Last.SetInteractable(_showPresetTab || _showHistoryTab);
            builder.AddButton("预设整合包", ShowPresetTab, "tab_presets").Last.SetInteractable(!_showPresetTab);
            builder.AddButton("切换记录", ShowHistoryTab, "tab_history").Last.SetInteractable(!_showHistoryTab);
            builder.End();

            if (_showHistoryTab)
                ChangeHistoryWindow.Build(builder, ProfileStore.ChangeHistory, RestartNoticeWindow.ShowStoredDetails);
            else if (_showPresetTab)
                PresetManagement.Build(builder, _draft, LoadPreset, Build);
            else
            {
                AddModSelectionTab(builder, availableMods, globalEnabledIds, listHeight);
                builder.AddSeparator(null, 6);
                builder.BeginRow(8f, TextAnchor.MiddleRight);
                builder.AddButton("保存配置", SaveCurrent, "save_profile");
                builder.End();
            }
            _window = builder.Center().Show();
            RefreshModSearchVisibility();
        }

        private static void AddModSelectionTab(CustomUIBuilder builder, List<LoadedMod> availableMods, HashSet<string> globalEnabledIds, float listHeight)
        {
            builder.AddHeader(GetSaveModsHeader());
            builder.AddLabel("默认只显示已加入全局清单的模组。显示未激活模组后，勾选项目会先经过游戏官方安全检查与授权。", "inactive_mods_help");
            StyleLastLabel(builder, new Color32(184, 180, 190, 255), 14f);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddToggle("显示未激活模组", _showInactiveMods, value =>
            {
                _showInactiveMods = value;
                Build();
            }, "show_inactive_mods");
            builder.AddLabel("排序");
            builder.Last.SetFixedWidth(54f);
            builder.AddDropdown(new List<string> { "按状态分组（默认）", "名称 A-Z", "名称 Z-A" }, _modSortMode, index =>
            {
                _modSortMode = Mathf.Clamp(index, 0, 2);
                Build();
            }, "mod_sort_mode");
            builder.Last.SetFixedWidth(260f);
            builder.AddInput("搜索名称/ID/作者/简介；@[作者关键字]", _modSearchDraft, null,
                "mod_search", UpdateModSearch);
            builder.Last.SetFlexible(1f, -1f);
            builder.End();
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                builder.AddLabel(_statusMessage, "save_status");
                if (builder.Last.text != null)
                    builder.Last.text.color = new Color32(117, 211, 117, 255);
                if (_statusNeedsRestart)
                {
                    builder.BeginRow(8f, TextAnchor.MiddleRight);
                    builder.AddButton("退出游戏以应用配置", ExitGame, "exit_to_apply");
                    builder.End();
                }
            }
            builder.AddSeparator(null, 4);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddLabel("启用");
            builder.Last.SetFixedWidth(48f);
            StyleLastLabel(builder, new Color32(153, 207, 201, 255), 16f);
            builder.AddLabel("模组名称");
            builder.Last.SetFixedWidth(330f);
            StyleLastLabel(builder, new Color32(153, 207, 201, 255), 16f);
            builder.AddLabel("版本");
            builder.Last.SetFixedWidth(125f);
            StyleLastLabel(builder, new Color32(153, 207, 201, 255), 16f);
            builder.AddLabel("作者");
            builder.Last.SetFixedWidth(260f);
            StyleLastLabel(builder, new Color32(153, 207, 201, 255), 16f);
            builder.End();
            builder.AddSeparator(null, 3);
            builder.BeginScroll(listHeight);
            _modListRows.Clear();
            _modGroupRows.Clear();
            _modSearchEmptyMessage = null;
            int previousGroup = -1;
            for (int i = 0; i < availableMods.Count; i++)
            {
                LoadedMod mod = availableMods[i];
                string id = mod.Manifest.ID;
                bool isManager = id == "save_mod_profiles";
                bool globallyAvailable = globalEnabledIds.Contains(id);
                int group = GetModStatusSortGroup(mod, globalEnabledIds);
                if (_modSortMode == 0 && group != previousGroup)
                {
                    if (previousGroup >= 0)
                        builder.AddSeparator(null, 3);
                    builder.BeginRow(8f, TextAnchor.MiddleLeft);
                    builder.AddLabel(GetModGroupLabel(group));
                    StyleLastLabel(builder, group == 2
                        ? new Color32(255, 199, 104, 255)
                        : new Color32(153, 207, 201, 255), 15f);
                    _modGroupRows[group] = GetRowContainer(builder.Last);
                    builder.End();
                    previousGroup = group;
                }
                builder.BeginRow(8f, TextAnchor.MiddleLeft);
                CustomUIElement toggle = null;
                builder.AddToggle("", globallyAvailable && _draft.Contains(id), value => ToggleMod(mod, globallyAvailable, toggle, value), "mod_toggle_" + i);
                toggle = builder.Last;
                toggle.SetFixedWidth(48f).SetInteractable(!isManager);
                builder.AddLabel(ModLoader.GetModLabel(mod.Manifest), "mod_name_" + i);
                builder.Last.SetFixedWidth(330f);
                StyleLastLabel(builder, globallyAvailable
                    ? new Color32(145, 210, 203, 255)
                    : new Color32(255, 199, 104, 255), 19f);
                builder.AddLabel(string.IsNullOrWhiteSpace(mod.Manifest.ModVersion) ? "未注明" : mod.Manifest.ModVersion, "mod_version_" + i);
                builder.Last.SetFixedWidth(125f);
                StyleLastLabel(builder, new Color32(184, 180, 190, 255), 16f);
                builder.AddLabel(string.IsNullOrWhiteSpace(mod.Manifest.Author) ? "未注明" : mod.Manifest.Author, "mod_author_" + i);
                builder.Last.SetFixedWidth(260f);
                StyleLastLabel(builder, new Color32(184, 180, 190, 255), 16f);
                GameObject mainRow = GetRowContainer(builder.Last);
                builder.End();
                builder.BeginRow(8f, TextAnchor.UpperLeft);
                string availability = globallyAvailable
                    ? ""
                    : HasOfficialApprovalRecord(mod)
                        ? "曾通过官方确认 · 当前未加入全局清单  ·  "
                        : "未激活过 · 勾选后需官方确认  ·  ";
                builder.AddLabel("ID：" + id + "  ·  " + availability + "简介：" + GetModDescription(mod.Manifest.Description), "mod_details_" + i);
                StyleLastLabel(builder, globallyAvailable
                    ? new Color32(151, 146, 157, 255)
                    : new Color32(235, 176, 104, 255), 14f);
                builder.Last.SetFlexible(1f, 1f);
                GameObject detailsRow = GetRowContainer(builder.Last);
                builder.End();
                _modListRows.Add(new ModListRow
                {
                    mod = mod,
                    group = group,
                    mainRow = mainRow,
                    detailsRow = detailsRow
                });
                builder.AddSeparator(null, 3);
            }
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddLabel("", "mod_search_empty");
            StyleLastLabel(builder, new Color32(184, 180, 190, 255), 15f);
            _modSearchEmptyMessage = builder.Last;
            builder.End();
            builder.End();
        }

        private sealed class ModListRow
        {
            internal LoadedMod mod;
            internal int group;
            internal GameObject mainRow;
            internal GameObject detailsRow;
        }

        private static GameObject GetRowContainer(CustomUIElement element)
        {
            return element == null || element.root == null || element.root.transform.parent == null
                ? null
                : element.root.transform.parent.gameObject;
        }

        private static void UpdateModSearch(string query)
        {
            _modSearchDraft = query ?? string.Empty;
            _modSearchText = _modSearchDraft.Trim();
            RefreshModSearchVisibility();
        }

        private static void RefreshModSearchVisibility()
        {
            if (_window == null || !_window.IsAlive)
                return;

            int visibleCount = 0;
            Dictionary<int, int> groupCounts = new Dictionary<int, int>();
            foreach (ModListRow row in _modListRows)
            {
                bool visible = MatchesModSearch(row.mod);
                SetRowVisible(row.mainRow, visible);
                SetRowVisible(row.detailsRow, visible);
                if (!visible)
                    continue;

                visibleCount++;
                int count;
                groupCounts.TryGetValue(row.group, out count);
                groupCounts[row.group] = count + 1;
            }

            foreach (KeyValuePair<int, GameObject> groupRow in _modGroupRows)
            {
                int count;
                SetRowVisible(groupRow.Value, groupCounts.TryGetValue(groupRow.Key, out count) && count > 0);
            }

            if (_modSearchEmptyMessage != null)
            {
                bool showEmpty = visibleCount == 0;
                _modSearchEmptyMessage.SetText(string.IsNullOrWhiteSpace(_modSearchText)
                    ? "没有可显示的模组。开启“显示未激活模组”可查找其他模组。"
                    : "没有找到匹配的模组。支持名称、ID、作者和简介；用 @[作者关键词] 只搜索作者。");
                _modSearchEmptyMessage.SetVisible(showEmpty);
            }
        }

        private static void SetRowVisible(GameObject row, bool visible)
        {
            if (row != null && row.activeSelf != visible)
                row.SetActive(visible);
        }

        private static bool MatchesModSearch(LoadedMod mod)
        {
            string query = (_modSearchText ?? string.Empty).Trim();
            if (query.Length == 0)
                return true;

            if (query.StartsWith("@[", StringComparison.Ordinal))
            {
                int closingBracket = query.IndexOf(']', 2);
                string authorQuery = closingBracket < 0
                    ? query.Substring(2).Trim()
                    : query.Substring(2, closingBracket - 2).Trim();
                return ContainsSearchTerm(mod.Manifest.Author, authorQuery);
            }

            return ContainsSearchTerm(GetModLabel(mod), query)
                || ContainsSearchTerm(mod.Manifest.ID, query)
                || ContainsSearchTerm(mod.Manifest.Author, query)
                || ContainsSearchTerm(mod.Manifest.Description, query);
        }

        private static bool ContainsSearchTerm(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(query)
                && !string.IsNullOrEmpty(value)
                && value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static int GetModStatusSortGroup(LoadedMod mod, HashSet<string> globallyEnabledIds)
        {
            string id = mod.Manifest.ID;
            if (globallyEnabledIds.Contains(id) && _draft.Contains(id))
                return 0;

            bool previouslyEnabledOrApproved = globallyEnabledIds.Contains(id) || HasOfficialApprovalRecord(mod);
            return previouslyEnabledOrApproved ? 1 : 2;
        }

        private static bool HasOfficialApprovalRecord(LoadedMod mod)
        {
            return mod.TrustRecord != null && !string.IsNullOrWhiteSpace(mod.TrustRecord.approvedAt);
        }

        private static string GetModGroupLabel(int group)
        {
            switch (group)
            {
                case 0: return "已勾选";
                case 1: return "未勾选（已启用或通过过官方确认）";
                default: return "未启用过（首次勾选需官方确认）";
            }
        }

        private static string GetModLabel(LoadedMod mod)
        {
            return ModLoader.GetModLabel(mod.Manifest);
        }

        private static void ToggleMod(LoadedMod mod, bool globallyAvailable, CustomUIElement toggle, bool enabled)
        {
            string id = mod.Manifest.ID;
            if (!enabled)
            {
                SetDraft(id, false);
                return;
            }

            if (globallyAvailable)
            {
                SetDraft(id, true);
                return;
            }

            toggle?.SetBool(false, false);
            RequestOfficialEnable(mod);
        }

        private static void RequestOfficialEnable(LoadedMod mod)
        {
            if (BeginEnableMethod == null || ModUIManager.Instance == null)
            {
                _statusMessage = "无法打开游戏官方安全确认窗口。请先在游戏原版模组菜单启用此模组，再返回此处配置。";
                _statusNeedsRestart = false;
                Build();
                return;
            }

            try
            {
                _awaitingOfficialConfirmation = true;
                HideGameWindowForOfficialConfirmation();
                if (_window != null && _window.IsAlive)
                    _window.Hide();

                BeginEnableMethod.Invoke(ModUIManager.Instance, new object[]
                {
                    mod,
                    (Action)(() => OnOfficialEnableApproved(mod))
                });
                if (_pendingApprovalRefresh)
                    RestoreAfterOfficialConfirmationClosed();
            }
            catch (Exception exception)
            {
                RestoreAfterOfficialConfirmationClosed();
                Debug.LogError("[SaveModProfiles] 调用游戏官方模组授权流程失败，异常类型：" + exception.GetType().Name);
                _statusMessage = "游戏官方安全确认无法启动。请从游戏原版模组菜单启用此模组。";
                _statusNeedsRestart = false;
                Build();
            }
        }

        internal static void RestoreAfterOfficialConfirmationClosed()
        {
            if (!_awaitingOfficialConfirmation && !_pendingApprovalRefresh)
                return;

            bool rebuild = _pendingApprovalRefresh;
            _awaitingOfficialConfirmation = false;
            _pendingApprovalRefresh = false;
            RestoreGameWindowAfterOfficialConfirmation();
            if (rebuild || _window == null || !_window.IsAlive)
            {
                Build();
                return;
            }

            _window.Show();
            _window.BringToFront();
        }

        private static void HideGameWindowForOfficialConfirmation()
        {
            Type windowOwner = _isNewGame ? typeof(NewGameSpecializationUI) : typeof(LoadGameUI);
            CustomUIWindow gameWindow = AccessTools.Field(windowOwner, "window")?.GetValue(null) as CustomUIWindow;
            if (gameWindow == null || !gameWindow.IsAlive)
                return;

            _gameWindowHiddenForOfficialConfirmation = gameWindow;
            gameWindow.Hide();
        }

        private static void RestoreGameWindowAfterOfficialConfirmation()
        {
            CustomUIWindow gameWindow = _gameWindowHiddenForOfficialConfirmation;
            _gameWindowHiddenForOfficialConfirmation = null;
            if (gameWindow != null && gameWindow.IsAlive)
                gameWindow.Show();
        }

        private static void OnOfficialEnableApproved(LoadedMod mod)
        {
            try
            {
                ProfileStore.ReconcileGlobalList();
                string id = mod.Manifest.ID;
                if (!ProfileStore.GlobalEnabledIds.Contains(id))
                {
                    _statusMessage = "游戏没有将此模组加入全局清单，请在原版模组菜单检查启用状态。";
                    _statusNeedsRestart = false;
                    return;
                }

                _draft.Add(id);
                _statusMessage = "已通过游戏官方确认并加入本次勾选。点击“保存配置”应用到此存档；如需重启，会在保存后提示。";
                _statusNeedsRestart = false;
            }
            catch (Exception exception)
            {
                Debug.LogError("[SaveModProfiles] 官方启用完成后同步存档模组清单失败，异常类型：" + exception.GetType().Name);
                _statusMessage = "模组已通过游戏官方确认；请检查全局清单后再保存存档配置。";
                _statusNeedsRestart = false;
            }
            finally
            {
                _pendingApprovalRefresh = true;
            }
        }

        private static string GetSaveModsHeader()
        {
            if (_isNewGame)
                return "新游戏使用的模组";

            SavePreviewData preview = SaveFiles.IndexLoad<SavePreviewData>("preview_" + _slotId, null);
            string saveName = preview == null ? null : preview.GetDisplayName();
            return string.IsNullOrWhiteSpace(saveName)
                ? "存档 " + _slotId + " 使用的模组"
                : saveName + " 使用的模组";
        }

        private static string GetModDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "未提供简介";
            string compact = description.Replace("\r", " ").Replace("\n", " ").Trim();
            return compact.Length <= 80 ? compact : compact.Substring(0, 77) + "…";
        }

        private static void StyleLastLabel(CustomUIBuilder builder, Color32 color, float fontSize)
        {
            if (builder.Last.text == null)
                return;
            builder.Last.text.color = color;
            builder.Last.text.fontSize = fontSize;
        }

        private static void ShowModTab()
        {
            _showPresetTab = false;
            _showHistoryTab = false;
            Build();
        }

        private static void ShowPresetTab()
        {
            _showPresetTab = true;
            _showHistoryTab = false;
            Build();
        }

        private static void ShowHistoryTab()
        {
            _showPresetTab = false;
            _showHistoryTab = true;
            Build();
        }

        internal static void ShowHistory()
        {
            ShowHistoryTab();
        }

        private static void SetDraft(string id, bool enabled)
        {
            if (enabled)
                _draft.Add(id);
            else
                _draft.Remove(id);
        }

        private static void LoadPreset(ModPreset preset)
        {
            _draft = new HashSet<string>(preset.modIds);
            PresetManagement.ClearStatus();
            _showPresetTab = false;
            _statusMessage = "已载入预设“" + preset.name + "”。请检查模组勾选，再点击“保存配置”应用到此存档。";
            Build();
        }

        private static void SaveCurrent()
        {
            PresetManagement.ClearStatus();
            string dependencyWarning = MissingDependencyMessage();
            List<string> excluded;
            bool restart;
            if (_isNewGame)
            {
                ProfileStore.SavePendingNewGame(_draft);
                restart = ProfileStore.ApplyNewGameProfile(out excluded);
            }
            else
            {
                ProfileStore.SaveProfileIds(_slotId, _draft);
                restart = ProfileStore.ApplySaveProfile(_slotId, out excluded);
            }
            string details = excluded.Count == 0 ? "" : "\n全局已关闭、暂不能加载的模组：" + string.Join("、", excluded);
            string message = _isNewGame
                ? (restart
                    ? "新游戏模组配置已保存并应用。需要重启才能加载所选模组；重启后重新进入新游戏流程即可继续，配置会保留。"
                    : "新游戏模组配置已保存。当前已加载清单与所选清单一致，可以继续选择初始天赋并开始游戏。")
                : (restart
                    ? "配置已保存。模组清单与当前已加载清单不同；请重启游戏，再重新打开此配置页进入存档。"
                    : "配置已保存。当前加载的模组与此清单一致。")
                + details + dependencyWarning;
            SetStatus(message, restart);
        }

        private static void SetStatus(string message, bool needsRestart)
        {
            _statusMessage = message;
            _statusNeedsRestart = needsRestart;
            _showPresetTab = false;
            Build();
        }


        private static string MissingDependencyMessage()
        {
            List<string> missing = new List<string>();
            foreach (LoadedMod mod in ModLoader.Mods.Where(item => _draft.Contains(item.Manifest.ID)))
            {
                foreach (string requiredId in mod.Manifest.Prerequisites)
                {
                    if (!_draft.Contains(requiredId))
                        missing.Add(ModLoader.GetModLabel(mod.Manifest) + " → " + requiredId);
                }
            }
            return missing.Count == 0 ? "" : "\n缺少前置依赖：" + string.Join("、", missing);
        }

        internal static void ShowMessage(string message)
        {
            if (CustomUIManager.Instance == null)
            {
                Debug.LogWarning("[SaveModProfiles] 消息窗口不可用；未记录提示内容。");
                return;
            }

            if (_messageWindow != null && _messageWindow.IsAlive)
                _messageWindow.Close();
            CustomUIBuilder builder = CustomUIManager.Window(MessageWindowId, "提示", "overlay")
                .UseMetaPrefabs()
                .SetSize(620f, 300f)
                .SetCloseOnEscape();
            builder.AddWarning(message);
            builder.AddSeparator(null, 6);
            builder.BeginRow(8f, TextAnchor.MiddleCenter);
            builder.AddButton("稍后重启", CloseMessage, "restart_later");
            builder.AddButton("退出游戏", ExitGame, "exit_game");
            builder.End();
            _messageWindow = builder.Center().Show();
        }

        private static void CloseMessage()
        {
            if (_messageWindow != null && _messageWindow.IsAlive)
                _messageWindow.Close();
            _messageWindow = null;
        }

        internal static void ExitGame()
        {
            Application.Quit();
        }
    }
}
