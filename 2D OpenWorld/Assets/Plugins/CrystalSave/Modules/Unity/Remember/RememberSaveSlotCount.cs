#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Save Slot Count")]
	[DisallowMultipleComponent]
	[RememberCustomIcon("Assets/Plugins/CrystalSave/Editor/Gizmos/SaveSlot.png")]
	public sealed class RememberSaveSlotCount : SaveableComponent
	{
		[Header("Save Optimization")]
		[SerializeField] private bool skipSavingWhenUnchanged = true;

		private SaveSlotCountData cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;

		protected override void Awake()
		{
			base.Awake();

			if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
			{
				cachedSnapshot = CloneSnapshot(snapshot);
				hasCachedSnapshot = cachedSnapshot != null;
			}
			else
			{
				cachedSnapshot = null;
				hasCachedSnapshot = false;
			}
		}

		/* ─────────────────────────────────────────────────────────────── */
		#region SERIALIZATION

		protected override byte[] SerializeComponentData()
		{
			if (!TryCaptureCurrentState(out var snapshot))
			{
				if (skipSavingWhenUnchanged)
				{
					cachedSnapshot = null;
					hasCachedSnapshot = false;
				}

				return null;
			}

			if (skipSavingWhenUnchanged)
			{
				if (hasCachedSnapshot && AreEquivalent(snapshot, cachedSnapshot))
				{
					if (cachedSerializedData != null && cachedSerializedData.Length > 0)
					{
						return cachedSerializedData;
					}
				}

				cachedSnapshot = CloneSnapshot(snapshot);
				hasCachedSnapshot = cachedSnapshot != null;
			}

			byte[] serialized = Serializer.Serialize(snapshot);
			
			if (skipSavingWhenUnchanged)
			{
				cachedSerializedData = serialized;
			}

			return serialized;
		}

		protected override void DeserializeComponentData(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return;

			SaveSlotCountData data = Serializer.Deserialize<SaveSlotCountData>(bytes);
			if (data == null || data.SlotCount <= 0) return;

			// Apply the persisted slot count when loading
			if (SaveManager.Instance != null)
			{
				// Use UnityMainThreadDispatcher to execute immediately on the main thread
				// This ensures slot count is restored during the load operation
				UnityMainThreadDispatcher.Instance().Enqueue(async () =>
				{
					try
					{
						await SaveManager.Instance.SetSaveSlotCountAsync(data.SlotCount);
						Logger.Log($"RememberSaveSlotCount: Restored slot count to {data.SlotCount}.", LogCategory.SaveManager, LogLevel.Info);
					}
					catch (Exception ex)
					{
						Logger.Log($"RememberSaveSlotCount: Failed to restore slot count: {ex.Message}", LogCategory.SaveManager, LogLevel.Warning);
					}
				});
			}

			if (skipSavingWhenUnchanged)
			{
				cachedSnapshot = CloneSnapshot(data);
				hasCachedSnapshot = cachedSnapshot != null;
			}
		}

		#endregion

		private bool TryCaptureCurrentState(out SaveSlotCountData snapshot)
		{
			if (SaveManager.Instance == null)
			{
				snapshot = null;
				return false;
			}

			int currentCount = SaveManager.Instance.CurrentSaveSlotCount;
			
			if (currentCount <= 0)
			{
				snapshot = null;
				return false;
			}

			snapshot = new SaveSlotCountData { SlotCount = currentCount };
			return true;
		}

		private static bool AreEquivalent(SaveSlotCountData a, SaveSlotCountData b)
		{
			if (ReferenceEquals(a, b)) return true;
			if (a == null || b == null) return false;

			return a.SlotCount == b.SlotCount;
		}

		private static SaveSlotCountData CloneSnapshot(SaveSlotCountData source)
		{
			if (source == null) return null;

			return new SaveSlotCountData { SlotCount = source.SlotCount };
		}
	}

	/* ─────────────────────────────────────────────────────────────── */

	[MemoryPackable]
	public partial class SaveSlotCountData
	{
		public int SlotCount { get; set; }

		// Needed by MemoryPack
		[MemoryPackConstructor] public SaveSlotCountData() { }
	}
}
#endif
