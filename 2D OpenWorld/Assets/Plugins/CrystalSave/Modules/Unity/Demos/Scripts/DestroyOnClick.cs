#if ARAWN_REMEMBERME && MEMORYPACK
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Demo
{
	public class DestroyOnClick : MonoBehaviour 
	{
		[Tooltip("The GameObject to destroy when the button is clicked.")]
		public GameObject targetToDestroy;

		private void Awake()
		{
			Button button = GetComponent<Button>();
			if (button != null)
			{
				button.onClick.AddListener(OnButtonClick);
			}
			else
			{
				Debug.LogWarning("DestroyOnClick script is not attached to a UI Button.");
			}
		}

                private void OnButtonClick()
                {
                        if (targetToDestroy != null)
                        {
                                if (SaveManager.Instance != null)
                                {
                                        SaveManager.Instance.DestroyWithSnapshot(targetToDestroy);
                                }
                                else
                                {
                                        Debug.LogWarning("SaveManager instance is not available. Falling back to Destroy.");
                                        Destroy(targetToDestroy);
                                }
                        }
                        else
                        {
                                Debug.LogWarning("Target to destroy is not assigned.");
                        }
                }
	}
}

#endif