#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Demo
{
        public class RestoreObjectButton : MonoBehaviour
        {
                [Tooltip("Enter the unique ID of the object to restore.")]
                public string uniqueID;

                [Tooltip("Enter the Prefab Asset ID of the object to restore.")]
                public string prefabAssetID;

		private void Awake()
		{
			// Get the Button component and assign the onClick event
			Button button = GetComponent<Button>();
			if (button != null)
			{
				button.onClick.AddListener(OnRestoreButtonClick);
			}
			else
			{
				Debug.LogWarning("RestoreObjectButton script is not attached to a Button.");
			}
		}

		private void OnRestoreButtonClick()
		{
                    var mgr = SaveManager.Instance;
                    if (mgr == null)
                    {
                        Debug.LogWarning("RestoreObjectButton: SaveManager instance is null – cannot restore.", this);
                        return;
                    }

                    bool didAnything = false;

                    if (!string.IsNullOrEmpty(uniqueID))
                    {
                        mgr.RestoreDestroyedGameObject(uniqueID);
                        didAnything = true;
                    }

                    if (!string.IsNullOrEmpty(prefabAssetID))
                    {
                        mgr.RestoreDestroyedPrefabByAssetID(prefabAssetID);
                        didAnything = true;
                    }

                    if (!didAnything)
                    {
                        Debug.LogWarning("RestoreObjectButton: Neither Unique ID nor Prefab Asset ID provided in the Inspector.", this);
                    }
               }
       }
}

#endif