using System;
using System.Collections.Generic;
using System.Linq;
using SaveModProfiles.Data;
using UnityEngine;

namespace SaveModProfiles.UI
{
    internal static class PresetManagement
    {
        private static string _selectedName;
        private static string _nameDraft = "";
        private static string _status;
        private static bool _statusIsError;

        internal static bool HasStatus => !string.IsNullOrEmpty(_status);

        internal static void Reset()
        {
            _selectedName = null;
            _nameDraft = "";
            _status = null;
            _statusIsError = false;
        }

        internal static void ClearStatus()
        {
            _status = null;
            _statusIsError = false;
        }

        internal static void Build(CustomUIBuilder builder, HashSet<string> draft, Action<ModPreset> onLoad, Action rebuild)
        {
            CustomUIWindow window = builder.Handle;
            builder.AddHeader("选择或保存预设");
            builder.AddLabel("新建：填写名称并保存当前勾选。载入：选择预设后载入到模组配置页，再点“保存配置”应用到此存档。更新会用当前勾选覆盖所选预设。");
            AddStatus(builder);

            List<string> names = new List<string> { "选择玩家预设…" };
            names.AddRange(ProfileStore.Presets.Select(preset => preset.name));
            int selected = _selectedName == null ? 0 : Math.Max(0, names.IndexOf(_selectedName));

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddDropdown(names, selected, index => SelectPreset(index == 0 ? null : names[index], window), "preset_list");
            builder.Last.SetFlexible(1f, -1f);
            builder.AddInput("输入预设名称", _nameDraft, null, "preset_name", value => _nameDraft = value);
            builder.Last.SetFlexible(1f, -1f);
            builder.End();

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddButton("载入所选预设", () => ApplySelected(onLoad, rebuild), "preset_apply")
                .WithTooltip("载入到模组配置页后，还要点击底部的“保存配置”才会应用到当前存档。");
            builder.AddButton("保存当前勾选为新预设", () => AddPreset(draft, window, rebuild), "preset_add")
                .WithTooltip("先在输入框填写名称，再保存当前模组勾选。");
            builder.End();

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddButton("用当前勾选更新所选预设", () => UpdatePreset(draft, window, rebuild), "preset_update")
                .WithTooltip("覆盖所选预设；输入框中的名称也可用于重命名。");
            builder.AddButton("删除所选预设", () => DeletePreset(rebuild), "preset_delete");
            builder.End();
        }

        private static void AddStatus(CustomUIBuilder builder)
        {
            if (string.IsNullOrEmpty(_status))
                return;
            if (_statusIsError)
            {
                builder.AddWarning(_status);
                return;
            }

            builder.AddLabel("✓ " + _status, "preset_status");
            if (builder.Last.text != null)
                builder.Last.text.color = new Color32(117, 211, 117, 255);
        }

        private static void SelectPreset(string name, CustomUIWindow window)
        {
            _selectedName = ProfileStore.FindPreset(name) == null ? null : name;
            _nameDraft = _selectedName ?? "";
            window.Get("preset_name").SetString(_nameDraft);
        }

        private static void ApplySelected(Action<ModPreset> onLoad, Action rebuild)
        {
            ModPreset preset = ProfileStore.FindPreset(_selectedName);
            if (preset == null)
            {
                ShowStatus("请先从列表中选择一个预设。", true, rebuild);
                return;
            }

            onLoad(preset);
        }

        private static void AddPreset(IEnumerable<string> draft, CustomUIWindow window, Action rebuild)
        {
            string name = window.Get("preset_name").GetString();
            _nameDraft = name;
            if (!ProfileStore.AddPreset(name, draft))
            {
                ShowStatus("未保存：名称不能为空，且不能与现有预设重名。", true, rebuild);
                return;
            }

            _selectedName = name.Trim();
            _nameDraft = _selectedName;
            ShowStatus("已保存新预设“" + _selectedName + "”。", false, rebuild);
        }

        private static void UpdatePreset(IEnumerable<string> draft, CustomUIWindow window, Action rebuild)
        {
            if (ProfileStore.FindPreset(_selectedName) == null)
            {
                ShowStatus("请先从列表中选择要更新的预设。", true, rebuild);
                return;
            }

            string name = window.Get("preset_name").GetString();
            _nameDraft = name;
            if (!ProfileStore.UpdatePreset(_selectedName, name, draft))
            {
                ShowStatus("未更新：名称不能为空，也不能与其他预设重名；原预设保持不变。", true, rebuild);
                return;
            }

            _selectedName = name.Trim();
            _nameDraft = _selectedName;
            ShowStatus("已更新预设“" + _selectedName + "”。", false, rebuild);
        }

        private static void DeletePreset(Action rebuild)
        {
            if (ProfileStore.FindPreset(_selectedName) == null)
            {
                ShowStatus("请先从列表中选择要删除的预设。", true, rebuild);
                return;
            }

            string deletedName = _selectedName;
            ProfileStore.DeletePreset(_selectedName);
            _selectedName = null;
            _nameDraft = "";
            ShowStatus("已删除预设“" + deletedName + "”。", false, rebuild);
        }

        private static void ShowStatus(string message, bool isError, Action rebuild)
        {
            _status = message;
            _statusIsError = isError;
            rebuild();
        }
    }
}
