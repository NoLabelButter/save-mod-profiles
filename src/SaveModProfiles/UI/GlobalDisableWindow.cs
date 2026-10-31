using System.Collections.Generic;
using System.Linq;
using SaveModProfiles.Data;
using UnityEngine;

namespace SaveModProfiles.UI
{
    internal static class GlobalDisableWindow
    {
        private const string WindowId = "save_mod_profiles_global_disable";
        private static CustomUIWindow _window;
        private static ModUIManager _manager;
        private static string _modId;
        internal static void Open(ModUIManager manager, string modId)
        {
            ProfileStore.ReconcileGlobalList();
            ProfileStore.PrepareExistingSaveProfiles();
            _manager = manager;
            _modId = modId;
            Build();
        }

        private static void Build()
        {
            if (_window != null && _window.IsAlive)
                _window.Close();
            if (CustomUIManager.Instance == null)
            {
                Debug.LogError("[SaveModProfiles] 全局影响确认页无法打开：CustomUIManager 未就绪。");
                return;
            }

            List<string> presets = ProfileStore.PresetsUsing(_modId);
            float windowHeight = 320f + (presets.Count > 0 ? 42f : 0f);
            CustomUIBuilder builder = CustomUIManager.Window(WindowId, "全局关闭模组", "overlay")
                .UseMetaPrefabs()
                .SetSize(820f, windowHeight)
                .SetCloseOnEscape();
            builder.AddWarning("全局关闭后，所有使用此模组的存档和预设都暂时无法加载它；重新全局启用后可恢复。");
            builder.AddLabel("模组：" + GetModLabel(_modId));
            builder.AddLabel(presets.Count == 0
                ? "没有预设包含此模组。"
                : "预设也会受全局关闭影响（当前选择会保留，重新启用后恢复）：" + string.Join("、", presets));
            builder.AddLabel("全局关闭会影响所有存档和预设；需重启游戏后生效。");
            AddRichLabel(builder, "<color=#F06975><b>想在所有存档中关闭此模组？</b></color> 点击下方“全局关闭此模组”。", "disable_global_help");
            builder.AddSeparator(null, 6);
            builder.BeginRow(8f, TextAnchor.MiddleRight);
            builder.AddButton("全局关闭此模组", DisableGlobally, "disable_globally")
                .WithTooltip("从全局启用清单中关闭；所有存档和预设暂时无法加载此模组，配置选择会保留。");
            builder.AddButton("取消", Close, "cancel_global_disable");
            builder.End();
            _window = builder.Center().Show();
        }

        private static void AddRichLabel(CustomUIBuilder builder, string text, string id)
        {
            builder.AddLabel(text, id);
            if (builder.Last.text != null)
                builder.Last.text.richText = true;
        }

        private static void DisableGlobally()
        {
            string modId = _modId;
            ModUIManager manager = _manager;
            Close();
            ModLoader.SetModEnabled(modId, false);
            manager.PopulateLayoutList();
            ProfileStore.DisableGlobally(modId);
            ProfileStore.ReconcileGlobalList();
            ProfileWindow.ShowMessage("已全局关闭“" + GetModLabel(modId) + "”。受影响的存档和预设会保留原清单；重新全局启用后可恢复。需要重启游戏。");
        }

        private static void Close()
        {
            if (_window != null && _window.IsAlive)
                _window.Close();
            _window = null;
            _manager = null;
            _modId = null;
        }

        private static string GetModLabel(string id)
        {
            LoadedMod mod = ModLoader.Mods.FirstOrDefault(item => item.Manifest.ID == id);
            return mod == null ? id : ModLoader.GetModLabel(mod.Manifest);
        }
    }
}
