#if MEMORYPACK
using System;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Base EventArgs class for SaveManager events.
	/// </summary>
	public class SaveManagerEventArgs : EventArgs
	{
		public SaveSlot Slot { get; }

		public SaveManagerEventArgs(SaveSlot slot)
		{
			Slot = slot;
		}
	}

	/// <summary>
	/// EventArgs for Save and Load operations, including success status and messages.
	/// </summary>
	public class SaveLoadEventArgs : SaveManagerEventArgs
	{
		public bool Success { get; }
		public string Message { get; }

		public SaveLoadEventArgs(SaveSlot slot, bool success, string message)
			: base(slot)
		{
			Success = success;
			Message = message;
		}
	}

	/// <summary>
	/// EventArgs for RenameSlot operations, including old and new names.
	/// </summary>
	public class RenameSlotEventArgs : SaveManagerEventArgs
	{
		public string OldName { get; }
		public string NewName { get; }

		public RenameSlotEventArgs(SaveSlot slot, string oldName, string newName)
			: base(slot)
		{
			OldName = oldName;
			NewName = newName;
		}
	}

	/// <summary>
	/// EventArgs specifically for operation failures.
	/// </summary>
	public class OperationFailedEventArgs : SaveManagerEventArgs
	{
		public string OperationName { get; }
		public string ErrorMessage { get; }

		public OperationFailedEventArgs(SaveSlot slot, string operationName, string errorMessage)
			: base(slot)
		{
			OperationName = operationName;
			ErrorMessage = errorMessage;
		}
	}
}
#endif