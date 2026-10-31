using System.Collections.Generic;
using SaveModProfiles.Data;
using UnityEngine;

namespace SaveModProfiles.UI
{
    internal static class ChangeHistoryWindow
    {
        internal static void Build(CustomUIBuilder builder, IReadOnlyList<SaveProfileChangeRecord> history, System.Action<SaveProfileChangeRecord> showDetails)
        {
            builder.AddHeader("最近 5 次切换记录");
            builder.AddLabel("切换到另一个存档，或为存档应用了需要切换的模组清单时，会保存记录。每条记录包含当时的来源、目标和完整模组变化。");
            builder.AddSeparator(null, 6);
            if (history == null || history.Count == 0)
            {
                builder.AddLabel("暂无切换记录。切换到其他存档或更改存档模组清单后，详情会保留在这里。", "history_empty");
                return;
            }

            builder.BeginScroll(410f);
            for (int i = 0; i < history.Count; i++)
            {
                SaveProfileChangeRecord record = history[i];
                builder.BeginPanel(spacing: 6f, padding: 10f, id: "history_" + i);
                builder.AddLabel(record.recordedAt ?? "", "history_time_" + i);
                builder.BeginRow(8f, TextAnchor.MiddleLeft);
                builder.AddLabel((record.sourceName ?? "当前启动清单") + "  →  " + (record.targetName ?? "存档 " + record.targetSlotId), "history_route_" + i);
                builder.Last.SetFlexible(1f, 1f);
                builder.AddButton("查看详情", () => showDetails(record), "history_details_" + i);
                builder.End();
                builder.End();
                if (i + 1 < history.Count)
                    builder.AddSeparator(null, 4);
            }
            builder.End();
        }
    }
}
