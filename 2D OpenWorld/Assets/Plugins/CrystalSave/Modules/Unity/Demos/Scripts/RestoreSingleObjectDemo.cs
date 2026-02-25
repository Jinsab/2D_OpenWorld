// ©2025 Arawn – Crystal Save
// RestoreSingleObjectDemo.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Arawn.CrystalSave.Demo
{
	public sealed class RestoreSingleObjectDemo : MonoBehaviour 
	{
		[Header("Reference  (choose ONE)")]
		[Tooltip("Drag the live object that owns SaveablePrefab / RememberXX components")]
		public GameObject target;
		[Tooltip("…or paste its UniqueID here if the object is spawned later")]
		public string uniqueID;

		[Header("Restore Settings")]
		[Range(0, 20)]
		public int slotNumber = 0;     // 0 = autosave / first slot
		public bool restoreOnStart = false;
		
#if ENABLE_INPUT_SYSTEM
		[Tooltip("Key to trigger restore (New Input System)")]
		public Key hotKey = Key.R;
#else
		[Tooltip("Key to trigger restore (Legacy Input)")]
		public KeyCode hotKey = KeyCode.R;
#endif

		/* ───────────────────────────── UNITY ───────────────────────────── */
		async void Start()
		{
			if (restoreOnStart)
				await TryRestore();
		}

		async void Update()
		{
			if (IsHotKeyPressed())
				await TryRestore();
		}

		/* ──────────────────────────── HELPERS ──────────────────────────── */
		bool IsHotKeyPressed()
		{
#if ENABLE_INPUT_SYSTEM
			return Keyboard.current != null && Keyboard.current[hotKey].wasPressedThisFrame;
#else
			return Input.GetKeyDown(hotKey);
#endif
		}

		async Task TryRestore()
		{
			if (SaveManager.Instance == null)
			{
				Debug.LogWarning($"{name}: SaveManager not present.");
				return;
			}

			if (target != null)
			{
				await SaveManager.Instance.RestoreSingleGameObjectWithRetryAsync(target, slotNumber);
				return;
			}

			if (!string.IsNullOrWhiteSpace(uniqueID))
			{
				SaveManager.Instance.RestoreSingleGameObject(uniqueID, slotNumber);
				return;
			}

			Debug.LogWarning($"{name}: No Target or UniqueID assigned.");
		}
	}
}
#endif