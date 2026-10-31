using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SaveModProfiles.Data
{
    internal static class ProfileStore
    {
        private const string ManagerModId = "save_mod_profiles";
        private const string FileName = "SaveModProfiles.json";
        private static ProfileData _data = new ProfileData();
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        internal static IReadOnlyList<string> GlobalEnabledIds => _data.globalEnabledIds;
        internal static IReadOnlyList<string> PendingNewGameIds => _data.hasPendingNewGame ? _data.pendingNewGameIds : _data.globalEnabledIds;
        internal static string PendingNewGameName => _data.pendingNewGameName ?? "";
        internal static IReadOnlyList<SaveProfile> SaveProfiles => _data.saves;
        internal static IReadOnlyList<ModPreset> Presets => _data.presets;
        internal static IReadOnlyList<SaveProfileChangeRecord> ChangeHistory => _data.changeHistory;
        internal static int LastEnteredSlotId => _data.hasLastEnteredSave ? _data.lastEnteredSlotId : -1;

        internal static void CaptureStartupEnabledIds()
        {
            SaveProfileChangeTracker.CaptureStartupEnabledIds();
        }

        internal static string DifficultyIdForSave(int slotId)
        {
            SaveDifficulty difficulty = _data.saveDifficulties.FirstOrDefault(item => item.slotId == slotId);
            if (difficulty == null)
                return null;
            return string.IsNullOrEmpty(difficulty.difficultyId) && difficulty.isHardMode
                ? "hard"
                : difficulty.difficultyId;
        }

        internal static void SaveDifficultyForSlot(int slotId, string difficultyId, bool isHardMode)
        {
            if (slotId < 0)
                return;

            SaveDifficulty difficulty = _data.saveDifficulties.FirstOrDefault(item => item.slotId == slotId);
            if (difficulty == null)
            {
                difficulty = new SaveDifficulty { slotId = slotId };
                _data.saveDifficulties.Add(difficulty);
            }
            if (difficulty.difficultyId == difficultyId && difficulty.isHardMode == isHardMode)
                return;

            difficulty.difficultyId = difficultyId ?? "";
            difficulty.isHardMode = isHardMode;
            Save();
        }

        internal static void SetLastEnteredSave(int slotId)
        {
            if (slotId < 0 || (_data.hasLastEnteredSave && _data.lastEnteredSlotId == slotId))
                return;

            _data.lastEnteredSlotId = slotId;
            _data.hasLastEnteredSave = true;
            Save();
        }

        internal static void PrepareExistingSaveProfiles()
        {
            List<int> slots = SaveFiles.IndexLoad("activeSaveSlotId", new List<int>()) ?? new List<int>();
            foreach (int slotId in slots)
            {
                if (File.Exists(SaveFiles.AbsolutePath(SaveFiles.Slot(slotId))))
                    GetOrCreateSave(slotId);
            }
        }

        internal static void Load()
        {
            if (File.Exists(FilePath))
                _data = FromStorage(JsonUtility.FromJson<ProfileDataStorage>(File.ReadAllText(FilePath)));

            _data.globalEnabledIds = _data.globalEnabledIds ?? new List<string>();
            _data.lastAppliedIds = _data.lastAppliedIds ?? new List<string>();
            _data.saves = _data.saves ?? new List<SaveProfile>();
            _data.saveDifficulties = _data.saveDifficulties ?? new List<SaveDifficulty>();
            _data.presets = _data.presets ?? new List<ModPreset>();
            _data.changeHistory = _data.changeHistory ?? new List<SaveProfileChangeRecord>();
            if (_data.changeHistory.Count > 5)
                _data.changeHistory.RemoveRange(5, _data.changeHistory.Count - 5);
            _data.pendingNewGameIds = _data.pendingNewGameIds ?? new List<string>();
            _data.pendingNewGameName = _data.pendingNewGameName ?? "";
        }

        internal static void ReconcileGlobalList()
        {
            List<string> actual = ReadModOrder();
            bool resetBaselineToCurrentOrder = !_data.hasGlobalBaseline
                || (!_data.hasPendingNewGame && !HasExistingSaves());
            if (resetBaselineToCurrentOrder)
            {
                bool changed = !_data.hasGlobalBaseline || !SameSet(actual, _data.globalEnabledIds)
                    || _data.hasAppliedProfile || !SameSet(actual, _data.lastAppliedIds);
                _data.globalEnabledIds = new List<string>(actual);
                _data.hasGlobalBaseline = true;
                if (!_data.hasPendingNewGame)
                {
                    _data.lastAppliedIds = new List<string>(actual);
                    _data.hasAppliedProfile = false;
                }
                if (changed)
                    Save();
                return;
            }

            if (_data.hasAppliedProfile)
            {
                if (SameSet(actual, _data.lastAppliedIds))
                    return;

                _data.globalEnabledIds.RemoveAll(id => _data.lastAppliedIds.Contains(id) && !actual.Contains(id));
                foreach (string id in actual)
                {
                    if (!_data.lastAppliedIds.Contains(id) && !_data.globalEnabledIds.Contains(id))
                        _data.globalEnabledIds.Add(id);
                }
                _data.lastAppliedIds = actual;
                Save();
                return;
            }

            if (!SameSet(actual, _data.globalEnabledIds))
            {
                _data.globalEnabledIds = actual;
                Save();
            }
        }

        private static bool HasExistingSaves()
        {
            List<int> slots = SaveFiles.IndexLoad("activeSaveSlotId", new List<int>()) ?? new List<int>();
            return slots.Any(slotId => File.Exists(SaveFiles.AbsolutePath(SaveFiles.Slot(slotId))));
        }

        internal static SaveProfile GetOrCreateSave(int slotId)
        {
            SaveProfile profile = _data.saves.FirstOrDefault(item => item.slotId == slotId);
            if (profile == null)
            {
                profile = new SaveProfile { slotId = slotId, modIds = new List<string>(_data.globalEnabledIds) };
                EnsureManager(profile.modIds);
                _data.saves.Add(profile);
                Save();
            }
            profile.modIds = profile.modIds ?? new List<string>();
            EnsureManager(profile.modIds);
            return profile;
        }

        internal static void SaveProfileIds(int slotId, IEnumerable<string> ids)
        {
            SaveProfile profile = GetOrCreateSave(slotId);
            profile.modIds = Normalize(ids);
            EnsureManager(profile.modIds);
            Save();
        }

        internal static void CopySaveProfile(int sourceSlotId, int destinationSlotId)
        {
            SaveProfile source = GetOrCreateSave(sourceSlotId);
            SaveProfile destination = GetOrCreateSave(destinationSlotId);
            destination.modIds = new List<string>(source.modIds);
            SaveDifficulty sourceDifficulty = _data.saveDifficulties.FirstOrDefault(item => item.slotId == sourceSlotId);
            _data.saveDifficulties.RemoveAll(item => item.slotId == destinationSlotId);
            if (sourceDifficulty != null)
            {
                _data.saveDifficulties.Add(new SaveDifficulty
                {
                    slotId = destinationSlotId,
                    difficultyId = sourceDifficulty.difficultyId,
                    isHardMode = sourceDifficulty.isHardMode
                });
            }
            Save();
        }

        internal static void DeleteSaveProfile(int slotId)
        {
            _data.saves.RemoveAll(profile => profile.slotId == slotId);
            _data.saveDifficulties.RemoveAll(item => item.slotId == slotId);
            if (_data.hasLastEnteredSave && _data.lastEnteredSlotId == slotId)
            {
                _data.lastEnteredSlotId = -1;
                _data.hasLastEnteredSave = false;
            }
            Save();
        }

        internal static void SavePendingNewGame(IEnumerable<string> ids)
        {
            _data.pendingNewGameIds = Normalize(ids);
            EnsureManager(_data.pendingNewGameIds);
            _data.hasPendingNewGame = true;
            Save();
        }

        internal static void SavePendingNewGameName(string name)
        {
            _data.pendingNewGameName = name ?? "";
            Save();
        }

        internal static void AssignPendingNewGameProfile(int slotId)
        {
            if (slotId < 0)
            {
                return;
            }

            bool hasPendingProfile = _data.hasPendingNewGame;
            List<string> ids = hasPendingProfile
                ? new List<string>(_data.pendingNewGameIds)
                : null;
            string customName = (_data.pendingNewGameName ?? "").Trim();
            if (hasPendingProfile)
            {
                SaveProfileIds(slotId, ids);
            }
            else
            {
                GetOrCreateSave(slotId);
            }
            _data.hasPendingNewGame = false;
            _data.pendingNewGameIds.Clear();
            _data.pendingNewGameName = "";
            Save();
            if (customName.Length > 0)
                SaveFiles.RenameSlot(slotId, customName);
        }

        internal static string SaveNameForPreview(int slotId)
        {
            bool isThisNewGame = NewGameData.Instance != null
                && NewGameData.Instance.saveSlot < 0
                && PlayerStore.Instance != null
                && PlayerStore.Instance.saveSlotId == slotId;
            if (isThisNewGame && !string.IsNullOrWhiteSpace(_data.pendingNewGameName))
                return _data.pendingNewGameName.Trim();

            SavePreviewData preview = SaveFiles.IndexLoad<SavePreviewData>("preview_" + slotId, null);
            return preview == null ? "" : preview.customName;
        }

        internal static bool ApplySaveProfile(int slotId, out List<string> excludedIds)
        {
            return ApplySaveProfile(slotId, out excludedIds, out _);
        }

        internal static bool ApplySaveProfile(int slotId, out List<string> excludedIds, out SaveProfileChangeSnapshot change)
        {
            SaveProfile profile = GetOrCreateSave(slotId);
            change = SaveProfileChangeTracker.Capture(slotId, LastEnteredSlotId, profile, _data.globalEnabledIds);
            bool restart = ApplyIds(profile.modIds, out excludedIds);
            if (restart)
                SaveProfileChangeTracker.SetPendingSave(slotId, change.targetIds);
            if (restart || change.sourceSlotId != slotId)
                RecordChange(change);
            return restart;
        }

        internal static bool ApplyNewGameProfile(out List<string> excludedIds)
        {
            List<string> ids = _data.hasPendingNewGame
                ? _data.pendingNewGameIds
                : _data.globalEnabledIds;
            bool restart = ApplyIds(ids, out excludedIds);
            if (restart)
                SaveProfileChangeTracker.ClearPendingSave();
            return restart;
        }

        internal static void DisableGlobally(string modId)
        {
            _data.globalEnabledIds.RemoveAll(id => id == modId);
            Save();
        }

        internal static List<string> PresetsUsing(string modId) => _data.presets
            .Where(preset => preset.modIds.Contains(modId))
            .Select(preset => preset.name)
            .ToList();

        internal static bool AddPreset(string name, IEnumerable<string> ids)
        {
            string cleanName = (name ?? "").Trim();
            if (cleanName.Length == 0 || _data.presets.Any(preset => preset.name == cleanName))
                return false;

            List<string> modIds = Normalize(ids);
            EnsureManager(modIds);
            _data.presets.Add(new ModPreset { name = cleanName, modIds = modIds });
            Save();
            return true;
        }

        internal static bool UpdatePreset(string oldName, string newName, IEnumerable<string> ids)
        {
            ModPreset preset = FindPreset(oldName);
            if (preset == null)
                return false;

            string cleanName = (newName ?? "").Trim();
            if (cleanName.Length == 0 || _data.presets.Any(item => item.name == cleanName && item != preset))
                return false;

            preset.name = cleanName;
            preset.modIds = Normalize(ids);
            EnsureManager(preset.modIds);
            Save();
            return true;
        }

        internal static void DeletePreset(string name)
        {
            _data.presets.RemoveAll(preset => preset.name == name);
            Save();
        }

        internal static ModPreset FindPreset(string name) => _data.presets.FirstOrDefault(preset => preset.name == name);

        private static void RecordChange(SaveProfileChangeSnapshot change)
        {
            change.recordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            change.sourceName = change.sourceSlotId < 0 ? "当前启动清单" : SaveName(change.sourceSlotId);
            change.targetName = SaveName(change.targetSlotId);
            change.previousPendingName = change.previousPendingIds == null
                ? ""
                : (change.previousPendingSlotId < 0 ? "上一份待应用清单" : SaveName(change.previousPendingSlotId));
            _data.changeHistory.Insert(0, new SaveProfileChangeRecord
            {
                recordedAt = change.recordedAt,
                sourceSlotId = change.sourceSlotId,
                targetSlotId = change.targetSlotId,
                previousPendingSlotId = change.previousPendingSlotId,
                sourceName = change.sourceName,
                targetName = change.targetName,
                previousPendingName = change.previousPendingName,
                startupIds = new List<string>(change.startupIds),
                targetIds = new List<string>(change.targetIds),
                unavailableIds = new List<string>(change.unavailableIds),
                previousPendingIds = change.previousPendingIds == null ? null : new List<string>(change.previousPendingIds)
            });
            if (_data.changeHistory.Count > 5)
                _data.changeHistory.RemoveRange(5, _data.changeHistory.Count - 5);
            Save();
        }

        private static string SaveName(int slotId)
        {
            SavePreviewData preview = SaveFiles.IndexLoad<SavePreviewData>("preview_" + slotId, null);
            return preview == null ? "存档 " + slotId : preview.GetDisplayName();
        }

        private static bool ApplyIds(IEnumerable<string> profileIds, out List<string> excludedIds)
        {
            HashSet<string> enabled = new HashSet<string>(_data.globalEnabledIds);
            List<string> requested = Normalize(profileIds);
            excludedIds = requested.Where(id => !enabled.Contains(id)).ToList();
            List<string> target = _data.globalEnabledIds.Where(requested.Contains).Distinct().ToList();
            bool modSetChanged = !SameSet(ModLoader.EnabledMods, target);
            bool restartPending = ModLoader.RestartRequired;
            if (!modSetChanged && !restartPending)
                return false;

            WriteModOrder(target);
            _data.lastAppliedIds = target;
            _data.hasAppliedProfile = true;
            Save();
            ModLoader.MarkRestartRequired();
            return true;
        }

        private static List<string> ReadModOrder()
        {
            Type es3 = FindEs3Type();
            MethodInfo load = es3.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "Load" && method.IsGenericMethodDefinition && method.GetParameters().Length == 2
                    && method.GetParameters()[1].ParameterType == method.GetGenericArguments()[0]);
            return (List<string>)load.MakeGenericMethod(typeof(List<string>)).Invoke(null, new object[] { "modOrder", new List<string>() });
        }

        private static void WriteModOrder(List<string> ids)
        {
            Type es3 = FindEs3Type();
            MethodInfo save = es3.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "Save" && method.IsGenericMethodDefinition && method.GetParameters().Length == 2
                    && method.GetParameters()[1].ParameterType == method.GetGenericArguments()[0]);
            save.MakeGenericMethod(typeof(List<string>)).Invoke(null, new object[] { "modOrder", ids });
        }

        private static Type FindEs3Type() => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ES3", false))
            .First(type => type != null);

        private static List<string> Normalize(IEnumerable<string> ids) => ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        private static void EnsureManager(List<string> ids)
        {
            if (!ids.Contains(ManagerModId))
                ids.Add(ManagerModId);
        }

        private static bool SameSet(IEnumerable<string> left, IEnumerable<string> right)
        {
            HashSet<string> set = new HashSet<string>(left);
            return set.SetEquals(right);
        }

        private static void Save()
        {
            string json = JsonUtility.ToJson(ToStorage(), true);
            File.WriteAllText(FilePath, json);
        }

        private static ProfileData FromStorage(ProfileDataStorage stored)
        {
            if (stored == null)
                return new ProfileData();

            return new ProfileData
            {
                schemaVersion = stored.schemaVersion,
                hasGlobalBaseline = stored.hasGlobalBaseline,
                globalEnabledIds = stored.globalEnabledIds,
                lastAppliedIds = stored.lastAppliedIds,
                hasAppliedProfile = stored.hasAppliedProfile,
                lastEnteredSlotId = stored.hasLastEnteredSave ? stored.lastEnteredSlotId : -1,
                hasLastEnteredSave = stored.hasLastEnteredSave,
                saves = RestoreSaveProfiles(stored),
                saveDifficulties = RestoreSaveDifficulties(stored),
                presets = RestorePresets(stored),
                changeHistory = RestoreChangeHistory(stored),
                pendingNewGameIds = stored.pendingNewGameIds,
                hasPendingNewGame = stored.hasPendingNewGame,
                pendingNewGameName = stored.pendingNewGameName
            };
        }

        private static ProfileDataStorage ToStorage() => new ProfileDataStorage
        {
            schemaVersion = _data.schemaVersion,
            hasGlobalBaseline = _data.hasGlobalBaseline,
            globalEnabledIds = _data.globalEnabledIds,
            lastAppliedIds = _data.lastAppliedIds,
            hasAppliedProfile = _data.hasAppliedProfile,
            lastEnteredSlotId = _data.lastEnteredSlotId,
            hasLastEnteredSave = _data.hasLastEnteredSave,
            saveSlotIds = _data.saves.Select(profile => profile.slotId).ToArray(),
            saveModIds = _data.saves.Select(profile => string.Join("\n", profile.modIds)).ToArray(),
            difficultySlotIds = _data.saveDifficulties.Select(item => item.slotId).ToArray(),
            difficultyIds = _data.saveDifficulties.Select(item => item.difficultyId ?? "").ToArray(),
            difficultyHardModes = _data.saveDifficulties.Select(item => item.isHardMode).ToArray(),
            presetNames = _data.presets.Select(preset => preset.name).ToArray(),
            presetModIds = _data.presets.Select(preset => string.Join("\n", preset.modIds)).ToArray(),
            historyRecordedAt = _data.changeHistory.Select(item => item.recordedAt ?? "").ToArray(),
            historySourceSlotIds = _data.changeHistory.Select(item => item.sourceSlotId).ToArray(),
            historyTargetSlotIds = _data.changeHistory.Select(item => item.targetSlotId).ToArray(),
            historyPreviousPendingSlotIds = _data.changeHistory.Select(item => item.previousPendingSlotId).ToArray(),
            historySourceNames = _data.changeHistory.Select(item => item.sourceName ?? "").ToArray(),
            historyTargetNames = _data.changeHistory.Select(item => item.targetName ?? "").ToArray(),
            historyPreviousPendingNames = _data.changeHistory.Select(item => item.previousPendingName ?? "").ToArray(),
            historyStartupModIds = _data.changeHistory.Select(item => string.Join("\n", item.startupIds ?? new List<string>())).ToArray(),
            historyTargetModIds = _data.changeHistory.Select(item => string.Join("\n", item.targetIds ?? new List<string>())).ToArray(),
            historyUnavailableModIds = _data.changeHistory.Select(item => string.Join("\n", item.unavailableIds ?? new List<string>())).ToArray(),
            historyPreviousPendingModIds = _data.changeHistory.Select(item => item.previousPendingIds == null ? "" : string.Join("\n", item.previousPendingIds)).ToArray(),
            historyHasPreviousPending = _data.changeHistory.Select(item => item.previousPendingIds != null).ToArray(),
            pendingNewGameIds = _data.pendingNewGameIds,
            hasPendingNewGame = _data.hasPendingNewGame,
            pendingNewGameName = _data.pendingNewGameName
        };

        private static List<SaveProfile> RestoreSaveProfiles(ProfileDataStorage stored)
        {
            List<SaveProfile> profiles = new List<SaveProfile>();
            int[] slots = stored.saveSlotIds ?? new int[0];
            string[] modLists = stored.saveModIds ?? new string[0];
            for (int i = 0; i < slots.Length; i++)
                profiles.Add(new SaveProfile { slotId = slots[i], modIds = SplitIds(i < modLists.Length ? modLists[i] : "") });
            return profiles;
        }

        private static List<ModPreset> RestorePresets(ProfileDataStorage stored)
        {
            List<ModPreset> presets = new List<ModPreset>();
            string[] names = stored.presetNames ?? new string[0];
            string[] modLists = stored.presetModIds ?? new string[0];
            for (int i = 0; i < names.Length; i++)
                presets.Add(new ModPreset { name = names[i], modIds = SplitIds(i < modLists.Length ? modLists[i] : "") });
            return presets;
        }

        private static List<SaveDifficulty> RestoreSaveDifficulties(ProfileDataStorage stored)
        {
            List<SaveDifficulty> difficulties = new List<SaveDifficulty>();
            int[] slots = stored.difficultySlotIds ?? new int[0];
            string[] ids = stored.difficultyIds ?? new string[0];
            bool[] hardModes = stored.difficultyHardModes ?? new bool[0];
            for (int i = 0; i < slots.Length; i++)
            {
                difficulties.Add(new SaveDifficulty
                {
                    slotId = slots[i],
                    difficultyId = i < ids.Length ? ids[i] : "",
                    isHardMode = i < hardModes.Length && hardModes[i]
                });
            }
            return difficulties;
        }

        private static List<SaveProfileChangeRecord> RestoreChangeHistory(ProfileDataStorage stored)
        {
            if (stored.historyRecordedAt == null
                || (stored.historyRecordedAt.Length == 0 && stored.changeHistory != null && stored.changeHistory.Length > 0))
                return stored.changeHistory == null
                    ? new List<SaveProfileChangeRecord>()
                    : stored.changeHistory.ToList();

            List<SaveProfileChangeRecord> history = new List<SaveProfileChangeRecord>();
            for (int i = 0; i < stored.historyRecordedAt.Length; i++)
            {
                history.Add(new SaveProfileChangeRecord
                {
                    recordedAt = StringAt(stored.historyRecordedAt, i),
                    sourceSlotId = IntAt(stored.historySourceSlotIds, i, -1),
                    targetSlotId = IntAt(stored.historyTargetSlotIds, i, -1),
                    previousPendingSlotId = IntAt(stored.historyPreviousPendingSlotIds, i, -1),
                    sourceName = StringAt(stored.historySourceNames, i),
                    targetName = StringAt(stored.historyTargetNames, i),
                    previousPendingName = StringAt(stored.historyPreviousPendingNames, i),
                    startupIds = SplitIds(StringAt(stored.historyStartupModIds, i)),
                    targetIds = SplitIds(StringAt(stored.historyTargetModIds, i)),
                    unavailableIds = SplitIds(StringAt(stored.historyUnavailableModIds, i)),
                    previousPendingIds = BoolAt(stored.historyHasPreviousPending, i)
                        ? SplitIds(StringAt(stored.historyPreviousPendingModIds, i))
                        : null
                });
            }
            return history;
        }

        private static string StringAt(string[] values, int index) => values != null && index < values.Length ? values[index] ?? "" : "";
        private static int IntAt(int[] values, int index, int fallback) => values != null && index < values.Length ? values[index] : fallback;
        private static bool BoolAt(bool[] values, int index) => values != null && index < values.Length && values[index];

        private static List<string> SplitIds(string value) => string.IsNullOrEmpty(value)
            ? new List<string>()
            : value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

    }

    [Serializable]
    public sealed class ProfileDataStorage
    {
        public int schemaVersion = 1;
        public bool hasGlobalBaseline;
        public List<string> globalEnabledIds = new List<string>();
        public List<string> lastAppliedIds = new List<string>();
        public bool hasAppliedProfile;
        public int lastEnteredSlotId = -1;
        public bool hasLastEnteredSave;
        public int[] saveSlotIds = new int[0];
        public string[] saveModIds = new string[0];
        public int[] difficultySlotIds = new int[0];
        public string[] difficultyIds = new string[0];
        public bool[] difficultyHardModes = new bool[0];
        public string[] presetNames = new string[0];
        public string[] presetModIds = new string[0];
        public string[] historyRecordedAt = new string[0];
        public int[] historySourceSlotIds = new int[0];
        public int[] historyTargetSlotIds = new int[0];
        public int[] historyPreviousPendingSlotIds = new int[0];
        public string[] historySourceNames = new string[0];
        public string[] historyTargetNames = new string[0];
        public string[] historyPreviousPendingNames = new string[0];
        public string[] historyStartupModIds = new string[0];
        public string[] historyTargetModIds = new string[0];
        public string[] historyUnavailableModIds = new string[0];
        public string[] historyPreviousPendingModIds = new string[0];
        public bool[] historyHasPreviousPending = new bool[0];
        public SaveProfileChangeRecord[] changeHistory = new SaveProfileChangeRecord[0];
        public List<string> pendingNewGameIds = new List<string>();
        public bool hasPendingNewGame;
        public string pendingNewGameName = "";
    }
}
