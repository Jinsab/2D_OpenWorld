using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{

    [DefaultExecutionOrder(102)]
	[DisallowMultipleComponent]
	[AddComponentMenu("")]
	public class SceneObjectID : MonoBehaviour 
    {
        [SerializeField]
        [Tooltip("Unique identifier for this scene GameObject.")]
        private string uniqueID;

        [SerializeField]
        [Tooltip("If true, this GameObject is preserved across scene loads (DontDestroyOnLoad).")]
        protected bool keepAcrossScenes = false;
		public bool KeepAcrossScenes
		{
			get => keepAcrossScenes;
			set => keepAcrossScenes = value;
		}

		[SerializeField]
		[Tooltip("If true, this GameObject will be disabled in scenes other than its original scene.")]
		private bool disableOnSceneLoad = false;

		public string UniqueID => uniqueID;
		public bool DisableOnSceneLoad => disableOnSceneLoad;
#if UNITY_EDITOR
        private void Reset()
        {
            // Assign a GUID when the component is added in the editor
            if (string.IsNullOrEmpty(uniqueID))
            {
                uniqueID = Guid.NewGuid().ToString();
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
        }

        private void OnValidate()
        {
            // Fill missing ID during edits without scanning the entire hierarchy
            if (string.IsNullOrEmpty(uniqueID))
            {
                uniqueID = Guid.NewGuid().ToString();
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
        }
#endif
#if REMEMBERME_OBSOLETE_PRESENT
        private void Awake()
        {
            if (string.IsNullOrEmpty(uniqueID))
            {
                uniqueID = Guid.NewGuid().ToString();
                Logger.Log($"SceneObjectID: Assigned new uniqueID '{uniqueID}' to scene GameObject '{gameObject.name}'.");
            }

            // If keepAcrossScenes is true, persist this GameObject across scene loads
            if (keepAcrossScenes)
            {
				// Ensure this is a root GameObject before calling DontDestroyOnLoad
				if (transform.root == transform)
                {
                    DontDestroyOnLoad(gameObject);
                    Logger.Log($"SceneObjectID: '{gameObject.name}' set to DontDestroyOnLoad.", LogCategory.SceneManagement, LogLevel.Info);
                }
                else
                {
                    Logger.Log($"SceneObjectID: '{gameObject.name}' could not be made persistent because it's not a root object.", LogCategory.SceneManagement, LogLevel.Warning);
                }

				// Make persistent via PersistentManager
				PersistentManager.MakePersistent(gameObject, keepAcrossScenes);
			}


		}
#endif
	}
}
