#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Helper utility for setting animator triggers while ensuring they're properly tracked by RememberAnimator.
	/// Use this instead of calling animator.SetTrigger() directly when you need triggers to be recorded.
	/// </summary>
	public static class AnimatorTriggerHelper
	{
		/// <summary>
		/// Sets a trigger on the animator and notifies RememberAnimator for proper recording.
		/// Use this instead of animator.SetTrigger() when you want the trigger to be captured in snapshots.
		/// </summary>
		/// <param name="animator">The Animator component</param>
		/// <param name="triggerName">Name of the trigger parameter</param>
		public static void SetTrigger(Animator animator, string triggerName)
		{
			if (animator == null) return;
			
			// Notify RememberAnimator before setting the trigger
			var rememberAnimator = animator.GetComponent<RememberAnimator>();
			if (rememberAnimator != null)
			{
				rememberAnimator.NotifyTriggerSet(triggerName);
			}
			
			// Set the trigger
			animator.SetTrigger(triggerName);
		}
		
		/// <summary>
		/// Sets a trigger on the animator using a hash and notifies RememberAnimator for proper recording.
		/// Use this instead of animator.SetTrigger() when you want the trigger to be captured in snapshots.
		/// </summary>
		/// <param name="animator">The Animator component</param>
		/// <param name="triggerHash">Hash of the trigger parameter (use Animator.StringToHash)</param>
		public static void SetTrigger(Animator animator, int triggerHash)
		{
			if (animator == null) return;
			
			// Notify RememberAnimator before setting the trigger
			var rememberAnimator = animator.GetComponent<RememberAnimator>();
			if (rememberAnimator != null)
			{
				rememberAnimator.NotifyTriggerSet(triggerHash);
			}
			
			// Set the trigger
			animator.SetTrigger(triggerHash);
		}
	}
	
	/// <summary>
	/// Extension methods for Animator to make trigger recording more convenient.
	/// Adds SetTriggerRecorded() method that automatically notifies RememberAnimator.
	/// </summary>
	public static class AnimatorExtensions
	{
		/// <summary>
		/// Sets a trigger and automatically notifies RememberAnimator for recording.
		/// Use this instead of SetTrigger() when you want the trigger to be captured in snapshots.
		/// Example: animator.SetTriggerRecorded("Jump");
		/// </summary>
		public static void SetTriggerRecorded(this Animator animator, string triggerName)
		{
			AnimatorTriggerHelper.SetTrigger(animator, triggerName);
		}
		
		/// <summary>
		/// Sets a trigger using a hash and automatically notifies RememberAnimator for recording.
		/// Use this instead of SetTrigger() when you want the trigger to be captured in snapshots.
		/// Example: animator.SetTriggerRecorded(jumpHash);
		/// </summary>
		public static void SetTriggerRecorded(this Animator animator, int triggerHash)
		{
			AnimatorTriggerHelper.SetTrigger(animator, triggerHash);
		}
	}
}
#endif
