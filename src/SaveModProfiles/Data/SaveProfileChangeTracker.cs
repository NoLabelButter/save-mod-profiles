using System;
using System.Collections.Generic;
using System.Linq;

namespace SaveModProfiles.Data
{
    internal static class SaveProfileChangeTracker
    {
        private static bool _startupCaptured;
        private static List<string> _startupEnabledIds = new List<string>();
        private static int _pendingSaveSlotId = -1;
        private static List<string> _pendingSaveIds;

        internal static void CaptureStartupEnabledIds()
        {
            if (_startupCaptured)
                return;

            _startupEnabledIds = ModLoader.EnabledMods.OrderBy(id => id, StringComparer.Ordinal).ToList();
            _startupCaptured = true;
        }

        internal static SaveProfileChangeSnapshot Capture(int targetSlotId, int sourceSlotId, SaveProfile profile, List<string> globallyEnabledIds)
        {
            return new SaveProfileChangeSnapshot
            {
                sourceSlotId = sourceSlotId,
                targetSlotId = targetSlotId,
                startupIds = new List<string>(_startupEnabledIds),
                targetIds = globallyEnabledIds.Where(profile.modIds.Contains).Distinct(StringComparer.Ordinal).ToList(),
                unavailableIds = profile.modIds.Where(id => !globallyEnabledIds.Contains(id)).ToList(),
                previousPendingSlotId = _pendingSaveSlotId,
                previousPendingIds = _pendingSaveIds == null ? null : new List<string>(_pendingSaveIds)
            };
        }

        internal static void SetPendingSave(int slotId, IEnumerable<string> ids)
        {
            _pendingSaveSlotId = slotId;
            _pendingSaveIds = new List<string>(ids);
        }

        internal static void ClearPendingSave()
        {
            _pendingSaveSlotId = -1;
            _pendingSaveIds = null;
        }
    }
}
