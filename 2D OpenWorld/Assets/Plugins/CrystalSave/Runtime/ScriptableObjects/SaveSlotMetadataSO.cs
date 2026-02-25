#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_LOCALIZATION
using UnityEngine.Localization;
#endif

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "SaveSlotMetadata", menuName = "Crystal Save/Settings/Save Slot Metadata")]
    public class SaveSlotMetadataSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
#if UNITY_LOCALIZATION
            public LocalizedString value;
            public string GetValue() => value.GetLocalizedString();
#else
            public string value;
            public string GetValue() => value;
#endif
        }

    [Tooltip("Custom metadata stored with each save slot. Populate this list to drive Save UI labels (e.g., player level, quest, world location, XP) or to feed conflict-resolution rules.")]
        public List<Entry> entries = new();

        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>();
            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.key))
                    dict[e.key] = e.GetValue();
            }
            return dict;
        }
    }
}
#endif
