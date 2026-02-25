#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    public class ComponentKeyDiffWindow : EditorWindow
    {
        private const int MaxHeuristicSuggestions = 800;

        [Serializable]
        private sealed class HeuristicSuggestion
        {
            public string SavedKey;
            public string LiveKey;
            public string Reason;
        }

        [Serializable]
        private sealed class ComparisonReport
        {
            public int SlotNumber;
            public int SavedCount;
            public int LiveCount;
            public int MatchedCount;
            public List<string> SavedOnly = new List<string>();
            public List<string> LiveOnly = new List<string>();
            public List<string> Matched = new List<string>();
            public List<HeuristicSuggestion> Suggestions = new List<HeuristicSuggestion>();
            public Dictionary<string, string> LiveKeyLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            public DateTime CreatedUtc;
        }

        private int slotNumber = 1;
        private bool isBusy;
        private string statusMessage = "Run a comparison in Play Mode.";
        private MessageType statusType = MessageType.Info;
        private string filter = string.Empty;
        private Vector2 scroll;

        private bool showSummary = true;
        private bool showSavedOnly = true;
        private bool showLiveOnly = true;
        private bool showMatched;
        private bool showSuggestions = true;
        private bool showQuickGuide = true;
        private bool actionableView = true;
        private bool showRawKeys;
        private int previewRowLimit = 40;

        private ComparisonReport report;

        [MenuItem("Tools/Crystal Save/Runtime Debug/Component Key Diff")]
        public static void ShowWindow()
        {
            GetWindow<ComponentKeyDiffWindow>("Component Key Diff");
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawQuickGuide();

            using (new EditorGUI.DisabledScope(isBusy))
            {
                DrawActions();
            }

            if (isBusy)
            {
                Rect r = GUILayoutUtility.GetRect(18f, 18f, "TextField");
                EditorGUI.ProgressBar(r, 0.5f, "Comparing...");
                GUILayout.Space(6f);
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            if (report == null)
            {
                return;
            }

            DrawResults();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Component Key Diff", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Compares save-slot ComponentsData keys vs currently registered live component UniqueIdentifiers.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
        }

        private void DrawQuickGuide()
        {
            showQuickGuide = EditorGUILayout.Foldout(showQuickGuide, "Quick Guide", true);
            if (!showQuickGuide)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Migration workflow:\n" +
                "1) Create a save in the OLD build.\n" +
                "2) Open the NEW build with the same save files.\n" +
                "3) Enter Play Mode (with saveables registered), then click Compare Slot.\n" +
                "4) Focus on Saved-Only and Live-Only.\n" +
                "5) Use Suggestions as candidate key remaps in MigrateComponentKeyMappings (no-code).\n" +
                "Note: Compare Slot reads the slot file directly. No game restart is required.",
                MessageType.Info);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                slotNumber = EditorGUILayout.IntField(new GUIContent("Slot"), slotNumber);
                if (slotNumber < 1)
                {
                    slotNumber = 1;
                }

                if (GUILayout.Button("Compare Slot", GUILayout.Width(120f)))
                {
                    BeginSlotComparison();
                }

                if (GUILayout.Button("Compare CurrentSaveData", GUILayout.Width(170f)))
                {
                    CompareCurrentSaveData();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                filter = EditorGUILayout.TextField(new GUIContent("Filter"), filter);

                using (new EditorGUI.DisabledScope(report == null))
                {
                    if (GUILayout.Button("Copy Full Report", GUILayout.Width(130f)))
                    {
                        CopyFullReportToClipboard();
                    }

                    if (GUILayout.Button("Export Report", GUILayout.Width(110f)))
                    {
                        ExportReport();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(report == null))
                {
                    if (GUILayout.Button("Copy Saved-Only", GUILayout.Width(130f)))
                    {
                        CopyListToClipboard(report.SavedOnly, "saved-only keys");
                    }

                    if (GUILayout.Button("Copy Live-Only", GUILayout.Width(130f)))
                    {
                        CopyListToClipboard(report.LiveOnly, "live-only keys");
                    }

                    if (GUILayout.Button("Copy Suggestions", GUILayout.Width(130f)))
                    {
                        CopySuggestionsToClipboard();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                actionableView = EditorGUILayout.ToggleLeft("Actionable View (hide matched list)", actionableView, GUILayout.Width(235f));
                showRawKeys = EditorGUILayout.ToggleLeft("Show Raw IDs", showRawKeys, GUILayout.Width(105f));
                previewRowLimit = EditorGUILayout.IntSlider(new GUIContent("Preview Rows"), previewRowLimit, 10, 200);
            }
        }

        private void DrawResults()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            showSummary = EditorGUILayout.Foldout(showSummary, "Summary", true);
            if (showSummary)
            {
                DrawSummary();
            }

            showSavedOnly = EditorGUILayout.Foldout(showSavedOnly, $"Saved-Only ({report.SavedOnly.Count})", true);
            if (showSavedOnly)
            {
                DrawKeyList(report.SavedOnly, "Saved-Only");
            }

            showLiveOnly = EditorGUILayout.Foldout(showLiveOnly, $"Live-Only ({report.LiveOnly.Count})", true);
            if (showLiveOnly)
            {
                DrawKeyList(report.LiveOnly, "Live-Only");
            }

            if (!actionableView)
            {
                showMatched = EditorGUILayout.Foldout(showMatched, $"Matched ({report.MatchedCount})", true);
                if (showMatched)
                {
                    DrawKeyList(report.Matched, "Matched");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Matched list hidden in Actionable View.", EditorStyles.miniLabel);
            }

            showSuggestions = EditorGUILayout.Foldout(showSuggestions, $"Heuristic Suggestions ({report.Suggestions.Count})", true);
            if (showSuggestions)
            {
                DrawSuggestions();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Slot", report.SlotNumber.ToString());
            EditorGUILayout.LabelField("Compared At (UTC)", report.CreatedUtc.ToString("u"));
            EditorGUILayout.LabelField("Saved Keys", report.SavedCount.ToString());
            EditorGUILayout.LabelField("Live Registered Keys", report.LiveCount.ToString());
            EditorGUILayout.LabelField("Matched", report.MatchedCount.ToString());
            EditorGUILayout.LabelField("Saved-Only", report.SavedOnly.Count.ToString());
            EditorGUILayout.LabelField("Live-Only", report.LiveOnly.Count.ToString());
            EditorGUILayout.LabelField("Saved-Only Objects", CountDistinctObjectPrefixes(report.SavedOnly).ToString());
            EditorGUILayout.LabelField("Live-Only Objects", CountDistinctObjectPrefixes(report.LiveOnly).ToString());
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4f);
            DrawInterpretation();
        }

        private void DrawInterpretation()
        {
            string message = BuildInterpretationMessage(report);
            MessageType type = (report.SavedOnly.Count == 0 && report.LiveOnly.Count == 0)
                ? MessageType.Info
                : MessageType.Warning;
            EditorGUILayout.HelpBox(message, type);
        }

        private static string BuildInterpretationMessage(ComparisonReport value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(512);

            if (value.SavedOnly.Count == 0 && value.LiveOnly.Count == 0)
            {
                sb.Append("No component-key mismatches detected. ");
                sb.Append("This slot should map cleanly to current registered components.");
            }
            else
            {
                if (value.SavedOnly.Count > 0)
                {
                    sb.Append(value.SavedOnly.Count)
                      .Append(" saved-only key(s): old save data has no live component match ");
                    sb.Append("(usually removed/renamed components or changed object IDs). ");
                    sb.Append("Affected objects: ").Append(CountDistinctObjectPrefixes(value.SavedOnly)).Append(". ");
                }

                if (value.LiveOnly.Count > 0)
                {
                    sb.Append(value.LiveOnly.Count)
                      .Append(" live-only key(s): components exist now but were not in that old save ");
                    sb.Append("(they keep defaults unless migrated). ");
                    sb.Append("Affected objects: ").Append(CountDistinctObjectPrefixes(value.LiveOnly)).Append(". ");
                }

                if (value.Suggestions != null && value.Suggestions.Count > 0)
                {
                    sb.Append("Use Suggestions as candidate old->new key remaps.");
                }
            }

            sb.Append("\nCompare Slot reads the slot file directly; no restart is required.");
            return sb.ToString().Trim();
        }

        private void DrawKeyList(IReadOnlyCollection<string> keys, string sectionName)
        {
            EditorGUI.indentLevel++;

            if (keys == null || keys.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            string trimmedFilter = string.IsNullOrWhiteSpace(filter)
                ? string.Empty
                : filter.Trim();

            IEnumerable<string> query = keys;
            if (!string.IsNullOrEmpty(trimmedFilter))
            {
                query = query.Where(k => !string.IsNullOrEmpty(k) &&
                                         k.IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            List<string> list = query.ToList();
            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("(no entries match filter)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            int maxRows = Mathf.Clamp(previewRowLimit, 10, 500);
            if (list.Count > maxRows)
            {
                EditorGUILayout.HelpBox(
                    $"Showing first {maxRows} of {list.Count} {sectionName} entries. Use copy buttons for full output.",
                    MessageType.Info);
            }

            int drawCount = Mathf.Min(list.Count, maxRows);
            if (!showRawKeys)
            {
                EditorGUILayout.LabelField("Display: GO <id> | COMP <id> (hover row for full key).", EditorStyles.miniLabel);
            }

            for (int i = 0; i < drawCount; i++)
            {
                string key = list[i];
                if (showRawKeys)
                {
                    EditorGUILayout.SelectableLabel(key, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (TryGetLiveLabel(key, out string liveLabelRaw))
                    {
                        EditorGUILayout.LabelField(liveLabelRaw, EditorStyles.miniLabel);
                    }
                }
                else
                {
                    string readable = FormatReadableKey(key);
                    if (TryGetLiveLabel(key, out string liveLabel))
                    {
                        readable = readable + " | " + liveLabel;
                    }

                    EditorGUILayout.LabelField(new GUIContent(readable, key), EditorStyles.miniLabel);
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawSuggestions()
        {
            EditorGUI.indentLevel++;

            if (report.Suggestions == null || report.Suggestions.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            string trimmedFilter = string.IsNullOrWhiteSpace(filter)
                ? string.Empty
                : filter.Trim();

            IEnumerable<HeuristicSuggestion> query = report.Suggestions;
            if (!string.IsNullOrEmpty(trimmedFilter))
            {
                query = query.Where(s =>
                    (!string.IsNullOrEmpty(s.SavedKey) && s.SavedKey.IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(s.LiveKey) && s.LiveKey.IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(s.Reason) && s.Reason.IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            List<HeuristicSuggestion> list = query.ToList();
            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("(no suggestions match filter)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            int maxRows = Mathf.Clamp(previewRowLimit, 10, 500);
            if (list.Count > maxRows)
            {
                EditorGUILayout.HelpBox(
                    $"Showing first {maxRows} of {list.Count} suggestion entries. Copy Suggestions for full output.",
                    MessageType.Info);
            }

            int drawCount = Mathf.Min(list.Count, maxRows);
            for (int i = 0; i < drawCount; i++)
            {
                HeuristicSuggestion suggestion = list[i];
                EditorGUILayout.BeginVertical("box");
                if (showRawKeys)
                {
                    EditorGUILayout.LabelField("Saved", suggestion.SavedKey ?? "<null>");
                    EditorGUILayout.LabelField("Live", suggestion.LiveKey ?? "<null>");
                    if (TryGetLiveLabel(suggestion.LiveKey, out string liveRawLabel))
                    {
                        EditorGUILayout.LabelField("Live Context", liveRawLabel, EditorStyles.wordWrappedMiniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Saved", FormatReadableKey(suggestion.SavedKey));
                    string liveDisplay = FormatReadableKey(suggestion.LiveKey);
                    if (TryGetLiveLabel(suggestion.LiveKey, out string liveLabel))
                    {
                        liveDisplay = liveDisplay + " | " + liveLabel;
                    }

                    EditorGUILayout.LabelField("Live", liveDisplay, EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.LabelField("Reason", suggestion.Reason ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUI.indentLevel--;
        }

        private void BeginSlotComparison()
        {
            if (!Application.isPlaying)
            {
                statusMessage = "Enter Play Mode before running a slot comparison.";
                statusType = MessageType.Warning;
                return;
            }

            SaveManager manager = SaveManager.Instance;
            if (manager == null)
            {
                statusMessage = "SaveManager.Instance is null.";
                statusType = MessageType.Error;
                return;
            }

            isBusy = true;
            statusMessage = $"Comparing slot {slotNumber}...";
            statusType = MessageType.Info;

            Task<SaveData> loadTask;
            try
            {
                loadTask = manager.LoadSaveDataForSlotAsync(slotNumber);
            }
            catch (Exception ex)
            {
                isBusy = false;
                statusMessage = $"Failed to start slot load: {ex.Message}";
                statusType = MessageType.Error;
                return;
            }

            _ = FinishSlotComparisonAsync(loadTask, manager, slotNumber);
        }

        private async Task FinishSlotComparisonAsync(Task<SaveData> loadTask, SaveManager manager, int slot)
        {
            SaveData data = null;
            Exception error = null;

            try
            {
                data = await loadTask;
            }
            catch (Exception ex)
            {
                error = ex;
            }

            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                isBusy = false;

                if (error != null)
                {
                    statusMessage = $"Slot comparison failed: {error.Message}";
                    statusType = MessageType.Error;
                    Repaint();
                    return;
                }

                if (data == null)
                {
                    statusMessage = $"No SaveData found for slot {slot}.";
                    statusType = MessageType.Warning;
                    Repaint();
                    return;
                }

                BuildReport(slot, data, manager);
                Repaint();
            };
        }

        private void CompareCurrentSaveData()
        {
            if (!Application.isPlaying)
            {
                statusMessage = "Enter Play Mode before comparing CurrentSaveData.";
                statusType = MessageType.Warning;
                return;
            }

            SaveManager manager = SaveManager.Instance;
            if (manager == null)
            {
                statusMessage = "SaveManager.Instance is null.";
                statusType = MessageType.Error;
                return;
            }

            SaveData data = manager.CurrentSaveData;
            if (data == null)
            {
                statusMessage = "CurrentSaveData is null. Load a slot first, then compare.";
                statusType = MessageType.Warning;
                return;
            }

            BuildReport(slotNumber, data, manager);
        }

        private void BuildReport(int comparedSlot, SaveData data, SaveManager manager)
        {
            HashSet<string> savedKeys = CollectSavedKeys(data);
            HashSet<string> liveKeys = CollectLiveRegisteredKeys(manager, out Dictionary<string, string> liveKeyLabels);

            List<string> matched = savedKeys.Intersect(liveKeys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
            List<string> savedOnly = savedKeys.Except(liveKeys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
            List<string> liveOnly = liveKeys.Except(savedKeys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

            report = new ComparisonReport
            {
                SlotNumber = comparedSlot,
                SavedCount = savedKeys.Count,
                LiveCount = liveKeys.Count,
                MatchedCount = matched.Count,
                SavedOnly = savedOnly,
                LiveOnly = liveOnly,
                Matched = matched,
                Suggestions = BuildHeuristicSuggestions(savedOnly, liveOnly),
                LiveKeyLabels = liveKeyLabels,
                CreatedUtc = DateTime.UtcNow
            };

            statusMessage =
                $"Compared slot {report.SlotNumber}: matched={report.MatchedCount}, saved-only={report.SavedOnly.Count}, live-only={report.LiveOnly.Count}.";
            statusType = MessageType.Info;
        }

        private static HashSet<string> CollectSavedKeys(SaveData data)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            if (data?.ComponentsData == null)
            {
                return keys;
            }

            foreach (var kvp in data.ComponentsData)
            {
                string key = kvp.Key;
                if (!string.IsNullOrEmpty(key))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        private static HashSet<string> CollectLiveRegisteredKeys(SaveManager manager, out Dictionary<string, string> liveKeyLabels)
        {
            liveKeyLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            if (manager?.ComponentManager == null)
            {
                return keys;
            }

            IReadOnlyList<ISaveable> saveables = manager.ComponentManager.GetSaveableComponents();
            if (saveables == null)
            {
                return keys;
            }

            for (int i = 0; i < saveables.Count; i++)
            {
                ISaveable saveable = saveables[i];
                if (saveable == null)
                {
                    continue;
                }

                try
                {
                    string id = saveable.UniqueIdentifier;
                    if (!string.IsNullOrEmpty(id))
                    {
                        keys.Add(id);
                        if (!liveKeyLabels.ContainsKey(id))
                        {
                            liveKeyLabels[id] = BuildLiveLabel(saveable);
                        }
                    }
                }
                catch
                {
                    // Ignore invalid/destroyed entries during diagnostics.
                }
            }

            return keys;
        }

        private static string BuildLiveLabel(ISaveable saveable)
        {
            if (saveable == null)
            {
                return string.Empty;
            }

            if (saveable is MonoBehaviour mb)
            {
                string goName = mb.gameObject != null ? mb.gameObject.name : "UnknownGameObject";
                string componentType = mb.GetType().Name;
                string sceneName = mb.gameObject != null ? mb.gameObject.scene.name : string.Empty;

                if (!string.IsNullOrEmpty(sceneName))
                {
                    return $"{goName} [{componentType}] @ {sceneName}";
                }

                return $"{goName} [{componentType}]";
            }

            return saveable.GetType().Name;
        }

        private static List<HeuristicSuggestion> BuildHeuristicSuggestions(
            IReadOnlyList<string> savedOnly,
            IReadOnlyList<string> liveOnly)
        {
            List<HeuristicSuggestion> suggestions = new List<HeuristicSuggestion>();
            if (savedOnly == null || liveOnly == null || savedOnly.Count == 0 || liveOnly.Count == 0)
            {
                return suggestions;
            }

            Dictionary<string, List<string>> liveBySuffix = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Dictionary<string, List<string>> liveByPrefix = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < liveOnly.Count; i++)
            {
                string liveKey = liveOnly[i];
                string prefix = ExtractPrefix(liveKey);
                string suffix = ExtractSuffix(liveKey);

                if (!string.IsNullOrEmpty(prefix))
                {
                    if (!liveByPrefix.TryGetValue(prefix, out var list))
                    {
                        list = new List<string>();
                        liveByPrefix.Add(prefix, list);
                    }

                    list.Add(liveKey);
                }

                if (!string.IsNullOrEmpty(suffix))
                {
                    if (!liveBySuffix.TryGetValue(suffix, out var list))
                    {
                        list = new List<string>();
                        liveBySuffix.Add(suffix, list);
                    }

                    list.Add(liveKey);
                }
            }

            HashSet<string> dedupe = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < savedOnly.Count; i++)
            {
                if (suggestions.Count >= MaxHeuristicSuggestions)
                {
                    break;
                }

                string savedKey = savedOnly[i];
                string savedPrefix = ExtractPrefix(savedKey);
                string savedSuffix = ExtractSuffix(savedKey);

                if (!string.IsNullOrEmpty(savedSuffix) && liveBySuffix.TryGetValue(savedSuffix, out var suffixMatches))
                {
                    AddSuggestions(suggestions, dedupe, savedKey, suffixMatches,
                        "Same component suffix (ComponentID) - likely GameObject UniqueID changed.");
                }

                if (!string.IsNullOrEmpty(savedPrefix) && liveByPrefix.TryGetValue(savedPrefix, out var prefixMatches))
                {
                    AddSuggestions(suggestions, dedupe, savedKey, prefixMatches,
                        "Same GameObject prefix - likely component list changed (added/removed/replaced).", skipSameKey: true);
                }
            }

            if (suggestions.Count > MaxHeuristicSuggestions)
            {
                suggestions = suggestions.Take(MaxHeuristicSuggestions).ToList();
            }

            return suggestions;
        }

        private static void AddSuggestions(
            ICollection<HeuristicSuggestion> target,
            ISet<string> dedupe,
            string savedKey,
            IEnumerable<string> liveMatches,
            string reason,
            bool skipSameKey = false)
        {
            int added = 0;
            foreach (string liveKey in liveMatches)
            {
                if (string.IsNullOrEmpty(liveKey))
                {
                    continue;
                }

                if (skipSameKey && string.Equals(savedKey, liveKey, StringComparison.Ordinal))
                {
                    continue;
                }

                string dedupeKey = savedKey + "|" + liveKey + "|" + reason;
                if (!dedupe.Add(dedupeKey))
                {
                    continue;
                }

                target.Add(new HeuristicSuggestion
                {
                    SavedKey = savedKey,
                    LiveKey = liveKey,
                    Reason = reason
                });

                added++;
                if (added >= 6)
                {
                    break;
                }
            }
        }

        private static string ExtractPrefix(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            int underscore = key.IndexOf('_');
            return underscore <= 0 ? string.Empty : key.Substring(0, underscore);
        }

        private static string ExtractSuffix(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            int underscore = key.IndexOf('_');
            if (underscore < 0 || underscore + 1 >= key.Length)
            {
                return string.Empty;
            }

            return key.Substring(underscore + 1);
        }

        private static string FormatReadableKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "<empty>";
            }

            string prefix = ExtractPrefix(key);
            string suffix = ExtractSuffix(key);

            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
            {
                return key;
            }

            return $"GO {ShortId(prefix)} | COMP {ShortId(suffix)}";
        }

        private static string ShortId(string value, int head = 8, int tail = 6)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length <= head + tail + 3)
            {
                return value;
            }

            return value.Substring(0, head) + "..." + value.Substring(value.Length - tail);
        }

        private bool TryGetLiveLabel(string key, out string label)
        {
            label = null;
            if (string.IsNullOrEmpty(key) || report?.LiveKeyLabels == null)
            {
                return false;
            }

            return report.LiveKeyLabels.TryGetValue(key, out label) && !string.IsNullOrEmpty(label);
        }

        private static int CountDistinctObjectPrefixes(IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return 0;
            }

            HashSet<string> distinct = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string prefix = ExtractPrefix(keys[i]);
                if (!string.IsNullOrEmpty(prefix))
                {
                    distinct.Add(prefix);
                }
            }

            return distinct.Count;
        }

        private void CopyListToClipboard(IReadOnlyList<string> list, string label)
        {
            if (list == null || list.Count == 0)
            {
                statusMessage = $"No {label} to copy.";
                statusType = MessageType.Info;
                return;
            }

            GUIUtility.systemCopyBuffer = string.Join("\n", list);
            statusMessage = $"Copied {list.Count} {label} to clipboard.";
            statusType = MessageType.Info;
        }

        private void CopySuggestionsToClipboard()
        {
            if (report?.Suggestions == null || report.Suggestions.Count == 0)
            {
                statusMessage = "No suggestions to copy.";
                statusType = MessageType.Info;
                return;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < report.Suggestions.Count; i++)
            {
                HeuristicSuggestion s = report.Suggestions[i];
                sb.Append(s.SavedKey).Append(" => ").Append(s.LiveKey).Append(" | ").Append(s.Reason).AppendLine();
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
            statusMessage = $"Copied {report.Suggestions.Count} suggestions to clipboard.";
            statusType = MessageType.Info;
        }

        private void CopyFullReportToClipboard()
        {
            if (report == null)
            {
                statusMessage = "No report to copy.";
                statusType = MessageType.Info;
                return;
            }

            GUIUtility.systemCopyBuffer = BuildReportText(report);
            statusMessage = "Full report copied to clipboard.";
            statusType = MessageType.Info;
        }

        private void ExportReport()
        {
            if (report == null)
            {
                statusMessage = "No report to export.";
                statusType = MessageType.Info;
                return;
            }

            string fileName = $"CrystalSave_ComponentKeyDiff_Slot{report.SlotNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = EditorUtility.SaveFilePanel("Export Component Key Diff", Application.dataPath, fileName, "txt");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                File.WriteAllText(path, BuildReportText(report));
                statusMessage = $"Report exported: {path}";
                statusType = MessageType.Info;
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception ex)
            {
                statusMessage = $"Failed to export report: {ex.Message}";
                statusType = MessageType.Error;
            }
        }

        private static string BuildReportText(ComparisonReport value)
        {
            StringBuilder sb = new StringBuilder(8192);
            sb.AppendLine("Crystal Save - Component Key Diff Report");
            sb.AppendLine(new string('=', 44));
            sb.Append("Slot: ").AppendLine(value.SlotNumber.ToString());
            sb.Append("Created UTC: ").AppendLine(value.CreatedUtc.ToString("u"));
            sb.AppendLine();

            sb.AppendLine("Summary");
            sb.AppendLine("-------");
            sb.Append("Saved Keys: ").AppendLine(value.SavedCount.ToString());
            sb.Append("Live Registered Keys: ").AppendLine(value.LiveCount.ToString());
            sb.Append("Matched: ").AppendLine(value.MatchedCount.ToString());
            sb.Append("Saved-Only: ").AppendLine(value.SavedOnly.Count.ToString());
            sb.Append("Live-Only: ").AppendLine(value.LiveOnly.Count.ToString());
            sb.AppendLine();

            AppendSection(sb, "Saved-Only", value.SavedOnly);
            AppendSection(sb, "Live-Only", value.LiveOnly);
            AppendSection(sb, "Matched", value.Matched);

            sb.AppendLine("Heuristic Suggestions");
            sb.AppendLine("---------------------");
            if (value.Suggestions == null || value.Suggestions.Count == 0)
            {
                sb.AppendLine("(none)");
            }
            else
            {
                for (int i = 0; i < value.Suggestions.Count; i++)
                {
                    HeuristicSuggestion suggestion = value.Suggestions[i];
                    sb.Append("- ")
                      .Append(suggestion.SavedKey)
                      .Append(" => ")
                      .Append(suggestion.LiveKey)
                      .Append(" | ")
                      .AppendLine(suggestion.Reason);
                }
            }

            return sb.ToString();
        }

        private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<string> values)
        {
            sb.AppendLine(title);
            sb.AppendLine(new string('-', title.Length));

            if (values == null || values.Count == 0)
            {
                sb.AppendLine("(none)");
                sb.AppendLine();
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                sb.Append("- ").AppendLine(values[i]);
            }

            sb.AppendLine();
        }
    }
}
#endif
#endif
