#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Demo
{
	[DefaultExecutionOrder(100)]
	public class UserSettingsTestUI : MonoBehaviour 
	{
		[Header("UI Buttons")]
		[SerializeField] private Button saveButton;
		[SerializeField] private Button loadButton;

		private void Start()
		{
			// Set up button onClick listeners if buttons are assigned
			if (saveButton != null)
				saveButton.onClick.AddListener(OnSaveButtonClicked);
			else
				Debug.LogWarning("Save Button is not assigned!");

			if (loadButton != null)
				loadButton.onClick.AddListener(OnLoadButtonClicked);
			else
				Debug.LogWarning("Load Button is not assigned!");
		}

		private void OnSaveButtonClicked()
		{
			if (UserSettingsManager.Instance != null)
			{
				UserSettingsManager.Instance.SaveSettings();
				Debug.Log("User settings saved!");
			}
			else
			{
				Debug.LogWarning("UserSettingsManager instance not found!");
			}
		}

		private void OnLoadButtonClicked()
		{
			if (UserSettingsManager.Instance != null)
			{
				UserSettingsManager.Instance.LoadSettings();
				Debug.Log("User settings loaded!");
			}
			else
			{
				Debug.LogWarning("UserSettingsManager instance not found!");
			}
		}
	}
}
#endif
