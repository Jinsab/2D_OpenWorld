#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.UI
{
    /// <summary>
    /// ScriptableObject used to configure the visual appearance of
    /// <see cref="SaveSlotEntryUI"/> instances. Designers can tweak
    /// colors, sprites and layout values without modifying prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveSlotEntryTheme", menuName = "Crystal Save/UI/Create Save Slot Entry Theme")]
    public class SaveSlotEntryTheme : ScriptableObject
    {
#if UNITY_EDITOR
        public static event System.Action<SaveSlotEntryTheme> ThemeChanged;
#endif
        [Header("Background")]
        public Sprite backgroundSprite;
        public Color  backgroundColor = Color.white;

        [Header("Text")]
        public Color textColor = Color.white;
        [Tooltip("Color used for text on all buttons.")]
        public Color buttonTextColor = Color.white;

    [Header("Input Field")]
    [Tooltip("Override for input text color. Leave alpha 0 to fall back to Text Color.")]
    public Color inputTextColor = new Color(0, 0, 0, 0);
    [Tooltip("Override for input placeholder color. Leave alpha 0 to derive from Input Text Color at 60% alpha.")]
    public Color inputPlaceholderColor = new Color(0, 0, 0, 0);
    [Tooltip("Override for input caret color. Leave alpha 0 to fall back to Input Text Color.")]
    public Color inputCaretColor = new Color(0, 0, 0, 0);
    [Tooltip("Override for input selection highlight color. Leave alpha 0 to derive from Input Text Color at 35% alpha.")]
    public Color inputSelectionColor = new Color(0, 0, 0, 0);
    [Tooltip("Optional background sprite for the input field.")]
    public Sprite inputBackgroundSprite;
    [Tooltip("Override for input field background color. Leave alpha 0 to keep existing background color.")]
    public Color inputBackgroundColor = new Color(0, 0, 0, 0);

        [Header("Layout")]
        public Vector2 sizeDelta;
        public Vector3 positionOffset;

        [Header("Screenshot")]
        public Sprite screenshotPlaceholder;

        [Header("Button Sprites")]
        public Sprite renameButtonSprite;
        public Sprite loadButtonSprite;
        public Sprite saveButtonSprite;
        public Sprite deleteButtonSprite;
        public Sprite confirmDeleteButtonSprite;
        public Sprite cancelDeleteButtonSprite;

        [Header("Panel")]
        public Image panel;
        public Sprite panelSprite;
        public Color  panelColor = Color.white;

        [Header("Sync Overlay")]
        public Color overlayColor = new Color(0f, 0f, 0f, 0.5f);

#if UNITY_EDITOR
        void OnValidate()
            => ThemeChanged?.Invoke(this);

        /// <summary>
        /// Invokes <see cref="ThemeChanged"/>. Used by custom editors to refresh
        /// preview UIs.
        /// </summary>
        public void NotifyThemeChanged()
            => ThemeChanged?.Invoke(this);
#endif
    }
}
#endif
