using System;
using System.Collections.Generic;

namespace SaveModProfiles.Data
{
    [Serializable]
    public sealed class ProfileData
    {
        public int schemaVersion = 1;
        public bool hasGlobalBaseline;
        public List<string> globalEnabledIds = new List<string>();
        public List<string> lastAppliedIds = new List<string>();
        public bool hasAppliedProfile;
        public int lastEnteredSlotId = -1;
        public bool hasLastEnteredSave;
        public List<SaveProfile> saves = new List<SaveProfile>();
        public List<SaveDifficulty> saveDifficulties = new List<SaveDifficulty>();
        public List<ModPreset> presets = new List<ModPreset>();
        public List<SaveProfileChangeRecord> changeHistory = new List<SaveProfileChangeRecord>();
        public List<string> pendingNewGameIds = new List<string>();
        public bool hasPendingNewGame;
        public string pendingNewGameName = "";
    }

    [Serializable]
    public sealed class SaveProfile
    {
        public int slotId;
        public List<string> modIds = new List<string>();
    }

    [Serializable]
    public sealed class SaveDifficulty
    {
        public int slotId;
        public string difficultyId = "";
        public bool isHardMode;
    }

    [Serializable]
    public sealed class ModPreset
    {
        public string name;
        public List<string> modIds = new List<string>();
    }

    [Serializable]
    public sealed class SaveProfileChangeRecord
    {
        public string recordedAt = "";
        public int sourceSlotId = -1;
        public int targetSlotId = -1;
        public int previousPendingSlotId = -1;
        public string sourceName = "";
        public string targetName = "";
        public string previousPendingName = "";
        public List<string> startupIds = new List<string>();
        public List<string> targetIds = new List<string>();
        public List<string> unavailableIds = new List<string>();
        public List<string> previousPendingIds;

        internal SaveProfileChangeSnapshot ToSnapshot() => new SaveProfileChangeSnapshot
        {
            recordedAt = recordedAt,
            sourceSlotId = sourceSlotId,
            targetSlotId = targetSlotId,
            previousPendingSlotId = previousPendingSlotId,
            sourceName = sourceName,
            targetName = targetName,
            previousPendingName = previousPendingName,
            startupIds = new List<string>(startupIds ?? new List<string>()),
            targetIds = new List<string>(targetIds ?? new List<string>()),
            unavailableIds = new List<string>(unavailableIds ?? new List<string>()),
            previousPendingIds = previousPendingIds == null ? null : new List<string>(previousPendingIds)
        };
    }

    internal sealed class SaveProfileChangeSnapshot
    {
        internal string recordedAt;
        internal int sourceSlotId;
        internal int targetSlotId;
        internal string sourceName;
        internal string targetName;
        internal string previousPendingName;
        internal List<string> startupIds = new List<string>();
        internal List<string> targetIds = new List<string>();
        internal List<string> unavailableIds = new List<string>();
        internal int previousPendingSlotId = -1;
        internal List<string> previousPendingIds;
    }
}
