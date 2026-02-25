using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Optional configuration for granular logging control.
    /// When this asset exists and LogLevel is set to Info, only enabled categories will log Info messages.
    /// If this asset doesn't exist, all Info logs are shown (default behavior).
    /// </summary>
    [CreateAssetMenu(fileName = "LoggerConfig", menuName = "Crystal Save/Settings/Create Logger Config")]
    public class LoggerConfig : ScriptableObject
    {
        [Header("Remember Components - Core")]
        [Tooltip("Enable Info logging for RememberTransform component")]
        public bool rememberTransform = false;
        
        [Tooltip("Enable Info logging for RememberParent component")]
        public bool rememberParent = false;
        
        [Tooltip("Enable Info logging for RememberGameObject component")]
        public bool rememberGameObject = false;
        
        [Header("Remember Components - Physics")]
        [Tooltip("Enable Info logging for RememberCollider component")]
        public bool rememberCollider = false;
        
        [Tooltip("Enable Info logging for RememberCollider2D component")]
        public bool rememberCollider2D = false;
        
        [Tooltip("Enable Info logging for RememberRigidbody component")]
        public bool rememberRigidbody = false;
        
        [Tooltip("Enable Info logging for RememberJoint component")]
        public bool rememberJoint = false;
        
        [Tooltip("Enable Info logging for RememberHinges component")]
        public bool rememberHinges = false;
        
        [Tooltip("Enable Info logging for RememberCharacterController component")]
        public bool rememberCharacterController = false;
        
        [Header("Remember Components - Rendering")]
        [Tooltip("Enable Info logging for RememberMeshRenderer component")]
        public bool rememberMeshRenderer = false;
        
        [Tooltip("Enable Info logging for RememberSkinnedMeshRenderer component")]
        public bool rememberSkinnedMeshRenderer = false;
        
        [Tooltip("Enable Info logging for RememberMaterial component")]
        public bool rememberMaterial = false;
        
        [Tooltip("Enable Info logging for RememberLight component")]
        public bool rememberLight = false;
        
        [Tooltip("Enable Info logging for RememberCamera component")]
        public bool rememberCamera = false;
        
        [Tooltip("Enable Info logging for RememberParticleSystem component")]
        public bool rememberParticleSystem = false;
        
        [Header("Remember Components - Animation & Audio")]
        [Tooltip("Enable Info logging for RememberAnimator component")]
        public bool rememberAnimator = false;
        
        [Tooltip("Enable Info logging for RememberAudioSource component")]
        public bool rememberAudioSource = false;
        
        [Header("Remember Components - Navigation & Terrain")]
        [Tooltip("Enable Info logging for RememberNavmeshAgent component")]
        public bool rememberNavmeshAgent = false;
        
        [Tooltip("Enable Info logging for RememberTerrain component")]
        public bool rememberTerrain = false;
        
        [Tooltip("Enable Info logging for RememberTilemap component")]
        public bool rememberTilemap = false;
        
        [Header("Remember Components - UI")]
        [Tooltip("Enable Info logging for RememberUISlider component")]
        public bool rememberUISlider = false;
        
        [Tooltip("Enable Info logging for RememberUIToggle component")]
        public bool rememberUIToggle = false;
        
        [Header("Remember Components - TextMeshPro")]
        [Tooltip("Enable Info logging for RememberTMPText component")]
        public bool rememberTMPText = false;
        
        [Tooltip("Enable Info logging for RememberTMPButton component")]
        public bool rememberTMPButton = false;
        
        [Tooltip("Enable Info logging for RememberTMPDropdown component")]
        public bool rememberTMPDropdown = false;
        
        [Tooltip("Enable Info logging for RememberTMPInputField component")]
        public bool rememberTMPInputField = false;
        
        [Header("Remember Components - Other")]
        [Tooltip("Enable Info logging for RememberScenes component")]
        public bool rememberScenes = false;
        
        [Tooltip("Enable Info logging for RememberComposite component")]
        public bool rememberComposite = false;
        
        [Tooltip("Enable Info logging for RememberCustomComponents")]
        public bool rememberCustomComponents = false;

#if REMEMBERME_GC2CORE_PRESENT || REMEMBERME_GC2MODULE_PRESENT || REMEMBERME_GC2STATS_PRESENT || REMEMBERME_GC2INVENTORY_PRESENT || REMEMBERME_GC2MELEE_PRESENT || REMEMBERME_GC2SHOOTER_PRESENT || REMEMBERME_GC2QUESTS_PRESENT || REMEMBERME_GC2DIALOGUE_PRESENT
        [Tooltip("Enable Info logging for Game Creator 2 Remember components (Animim, Stats, etc)")]
        public bool rememberGameCreator2 = false;
#endif
        
        [Tooltip("Enable Info logging for other Remember components")]
        public bool rememberOther = false;
        
        [Header("Core Systems")]
        [Tooltip("Enable Info logging for SaveManager")]
        public bool saveManager = false;
        
        [Tooltip("Enable Info logging for ComponentManager")]
        public bool componentManager = false;
        
        [Tooltip("Enable Info logging for PrefabManager")]
        public bool prefabManager = false;
        
        [Tooltip("Enable Info logging for SaveablePrefab")]
        public bool saveablePrefab = false;
        
        [Tooltip("Enable Info logging for SaveableComponent")]
        public bool saveableComponent = false;
        
        [Tooltip("Enable Info logging for SaveSystem")]
        public bool saveSystem = false;
        
        [Tooltip("Enable Info logging for TrackedGameObjectProxy")]
        public bool trackedGameObjectProxy = false;
        
        [Tooltip("Enable Info logging for SaveSlotManager")]
        public bool saveSlotManager = false;
        
        [Tooltip("Enable Info logging for GameObjectTracker")]
        public bool gameObjectTracker = false;
        
        [Tooltip("Enable Info logging for ScreenshotManager")]
        public bool screenshotManager = false;
        
        [Tooltip("Enable Info logging for TextureManager")]
        public bool textureManager = false;
        
        [Tooltip("Enable Info logging for UserSettingsManager")]
        public bool userSettingsManager = false;
        
        [Tooltip("Enable Info logging for scene loading and transitions")]
        public bool sceneManagement = false;
        
        [Header("Extensions & Utilities")]
        [Tooltip("Enable Info logging for SaveManagerExtensions")]
        public bool saveManagerExtensions = false;
        
        [Tooltip("Enable Info logging for serialization/deserialization")]
        public bool serialization = false;
        
        [Tooltip("Enable Info logging for encryption/decryption")]
        public bool cryptography = false;
        
        [Tooltip("Enable Info logging for cloud save operations")]
        public bool cloudSave = false;
        
        [Header("Catch-All")]
        [Tooltip("Enable Info logging for uncategorized systems")]
        public bool other = true;
        
        /// <summary>
        /// Check if logging is enabled for a specific category
        /// </summary>
        public bool IsEnabled(LogCategory category)
        {
            return category switch
            {
                // Core
                LogCategory.RememberTransform => rememberTransform,
                LogCategory.RememberParent => rememberParent,
                LogCategory.RememberGameObject => rememberGameObject,
                
                // Physics
                LogCategory.RememberCollider => rememberCollider,
                LogCategory.RememberCollider2D => rememberCollider2D,
                LogCategory.RememberRigidbody => rememberRigidbody,
                LogCategory.RememberJoint => rememberJoint,
                LogCategory.RememberHinges => rememberHinges,
                LogCategory.RememberCharacterController => rememberCharacterController,
                
                // Rendering
                LogCategory.RememberMeshRenderer => rememberMeshRenderer,
                LogCategory.RememberSkinnedMeshRenderer => rememberSkinnedMeshRenderer,
                LogCategory.RememberMaterial => rememberMaterial,
                LogCategory.RememberLight => rememberLight,
                LogCategory.RememberCamera => rememberCamera,
                LogCategory.RememberParticleSystem => rememberParticleSystem,
                
                // Animation & Audio
                LogCategory.RememberAnimator => rememberAnimator,
                LogCategory.RememberAudioSource => rememberAudioSource,
                
                // Navigation & Terrain
                LogCategory.RememberNavmeshAgent => rememberNavmeshAgent,
                LogCategory.RememberTerrain => rememberTerrain,
                LogCategory.RememberTilemap => rememberTilemap,
                
                // UI
                LogCategory.RememberUISlider => rememberUISlider,
                LogCategory.RememberUIToggle => rememberUIToggle,
                
                // TextMeshPro
                LogCategory.RememberTMPText => rememberTMPText,
                LogCategory.RememberTMPButton => rememberTMPButton,
                LogCategory.RememberTMPDropdown => rememberTMPDropdown,
                LogCategory.RememberTMPInputField => rememberTMPInputField,
                
                // Other Remember Components
                LogCategory.RememberScenes => rememberScenes,
                LogCategory.RememberComposite => rememberComposite,
                LogCategory.RememberCustomComponents => rememberCustomComponents,
#if REMEMBERME_GC2CORE_PRESENT || REMEMBERME_GC2MODULE_PRESENT || REMEMBERME_GC2STATS_PRESENT || REMEMBERME_GC2INVENTORY_PRESENT || REMEMBERME_GC2MELEE_PRESENT || REMEMBERME_GC2SHOOTER_PRESENT || REMEMBERME_GC2QUESTS_PRESENT || REMEMBERME_GC2DIALOGUE_PRESENT
                LogCategory.RememberGameCreator2 => rememberGameCreator2,
#endif
                LogCategory.RememberOther => rememberOther,
                
                // Core Systems
                LogCategory.SaveManager => saveManager,
                LogCategory.ComponentManager => componentManager,
                LogCategory.PrefabManager => prefabManager,
                LogCategory.SaveablePrefab => saveablePrefab,
                LogCategory.SaveableComponent => saveableComponent,
                LogCategory.SaveSystem => saveSystem,
                LogCategory.TrackedGameObjectProxy => trackedGameObjectProxy,
                LogCategory.SceneManagement => sceneManagement,
                LogCategory.SaveSlotManager => saveSlotManager,
                LogCategory.GameObjectTracker => gameObjectTracker,
                LogCategory.ScreenshotManager => screenshotManager,
                LogCategory.TextureManager => textureManager,
                LogCategory.UserSettingsManager => userSettingsManager,
                
                // Extensions & Utilities
                LogCategory.SaveManagerExtensions => saveManagerExtensions,
                LogCategory.Serialization => serialization,
                LogCategory.Cryptography => cryptography,
                LogCategory.CloudSave => cloudSave,
                
                // Catch-all
                LogCategory.Other => other,
                _ => other
            };
        }
    }
    
    /// <summary>
    /// Categories for granular logging control
    /// </summary>
    public enum LogCategory
    {
        // Core Remember Components
        RememberTransform,
        RememberParent,
        RememberGameObject,
        
        // Physics Remember Components
        RememberCollider,
        RememberCollider2D,
        RememberRigidbody,
        RememberJoint,
        RememberHinges,
        RememberCharacterController,
        
        // Rendering Remember Components
        RememberMeshRenderer,
        RememberMeshFilter,
        RememberSkinnedMeshRenderer,
        RememberMaterial,
        RememberLight,
        RememberCamera,
        RememberParticleSystem,
        
        // Animation & Audio Remember Components
        RememberAnimator,
        RememberAudioSource,
        
        // Navigation & Terrain Remember Components
        RememberNavmeshAgent,
        RememberTerrain,
        RememberTilemap,
        
        // UI Remember Components
        RememberUISlider,
        RememberUIToggle,
        
        // TextMeshPro Remember Components
        RememberTMPText,
        RememberTMPButton,
        RememberTMPDropdown,
        RememberTMPInputField,
        
        // Other Remember Components
        RememberScenes,
        RememberComposite,
        RememberCustomComponents,
#if REMEMBERME_GC2CORE_PRESENT || REMEMBERME_GC2MODULE_PRESENT || REMEMBERME_GC2STATS_PRESENT || REMEMBERME_GC2INVENTORY_PRESENT || REMEMBERME_GC2MELEE_PRESENT || REMEMBERME_GC2SHOOTER_PRESENT || REMEMBERME_GC2QUESTS_PRESENT || REMEMBERME_GC2DIALOGUE_PRESENT
        RememberGameCreator2,
#endif
        RememberOther,
        
        // Core Systems
        SaveManager,
        ComponentManager,
        PrefabManager,
        SaveablePrefab,
        SaveableComponent,
        SaveSystem,
        TrackedGameObjectProxy,
        SceneManagement,
        SaveSlotManager,
        GameObjectTracker,
        ScreenshotManager,
        TextureManager,
        UserSettingsManager,
        
        // Extensions & Utilities
        SaveManagerExtensions,
        Serialization,
        Cryptography,
        CloudSave,
        
        // Catch-all
        Other
    }
}