using System;

namespace Arawn.CrystalSave.Runtime
{
	public enum SaveState
	{
		Idle,
		Loading,
		Saving,
		Error
	}

	public class SaveStateMachine
	{
		public SaveState CurrentState { get; private set; } = SaveState.Idle;

		public event Action<SaveState> OnStateChanged;

		public void TransitionTo(SaveState newState)
		{
			if (CurrentState == newState) return;

			CurrentState = newState;
			OnStateChanged?.Invoke(newState);
		}
	}
}
