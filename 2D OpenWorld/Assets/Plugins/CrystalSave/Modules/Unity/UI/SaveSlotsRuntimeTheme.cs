#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.UI
{
    /// <summary>
    /// ScriptableObject used to theme <see cref="SaveSlotsRuntimeUI"/> and
    /// its instantiated <see cref="SaveSlotEntryUI"/> elements.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveSlotsRuntimeTheme", menuName = "Crystal Save/UI/Create Save Slots Runtime Theme")]
    public class SaveSlotsRuntimeTheme : ScriptableObject
    {
#if UNITY_EDITOR
        public static event System.Action<SaveSlotsRuntimeTheme> ThemeChanged;
#endif
        [Header("Background")]
        public Sprite backgroundSprite;
        public Color  backgroundColor = Color.white;

        [Header("Panel")]
        public Image panel;
        public Sprite panelSprite;
        public Color  panelColor = Color.white;

        [Header("Title")]
        public Image titleBackgroundPanel;
        public Color titleBackgroundColor = Color.white;
        public Sprite titleBackgroundSprite;
        public Text  titleSaveGameText;
        public Color titleTextColor = Color.white;

        [Header("Entry Theme")]
        public SaveSlotEntryTheme entryTheme;

#if UNITY_EDITOR
        void OnValidate()
            => ThemeChanged?.Invoke(this);

        /// <summary>
        /// Invokes <see cref="ThemeChanged"/>. This is called from custom
        /// editors to refresh preview UIs.
        /// </summary>
        public void NotifyThemeChanged()
            => ThemeChanged?.Invoke(this);
#endif
    }
}
#endif
