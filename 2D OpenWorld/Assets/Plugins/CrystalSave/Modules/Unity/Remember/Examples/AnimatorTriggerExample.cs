#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime.Examples
{
	/// <summary>
	/// Example demonstrating how to properly set animator triggers so they're recorded by RememberAnimator.
	/// </summary>
	public class AnimatorTriggerExample : MonoBehaviour
	{
		[SerializeField] private Animator animator;
		[SerializeField] private RememberAnimator rememberAnimator;
		
		private void Start()
		{
			if (animator == null)
			{
				animator = GetComponent<Animator>();
			}
			
			if (rememberAnimator == null)
			{
				rememberAnimator = GetComponent<RememberAnimator>();
			}
		}
		
		// ===== METHOD 1: Extension Method (Recommended - cleanest) =====
		public void JumpWithExtensionMethod()
		{
			// Simply call SetTriggerRecorded instead of SetTrigger
			// This automatically notifies RememberAnimator
			animator.SetTriggerRecorded("Jump");
		}
		
		// ===== METHOD 2: Helper Class =====
		public void JumpWithHelperClass()
		{
			// Use the static helper class
			AnimatorTriggerHelper.SetTrigger(animator, "Jump");
		}
		
		// ===== METHOD 3: Manual Notification =====
		public void JumpWithManualNotification()
		{
			// Manually notify RememberAnimator before setting the trigger
			if (rememberAnimator != null)
			{
				rememberAnimator.NotifyTriggerSet("Jump");
			}
			animator.SetTrigger("Jump");
		}
		
		// ===== METHOD 4: Using Hash (Better Performance) =====
		private int jumpHash;
		
		private void Awake()
		{
			// Cache the hash for better performance
			jumpHash = Animator.StringToHash("Jump");
		}
		
		public void JumpWithHash()
		{
			// Use the hash version for better performance
			animator.SetTriggerRecorded(jumpHash);
		}
		
		// ===== EXAMPLES IN COMMON SCENARIOS =====
		
		// Example: Triggered by input
		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				// Wrong way (trigger won't be recorded):
				// animator.SetTrigger("Jump");
				
				// Correct way (trigger will be recorded):
				animator.SetTriggerRecorded("Jump");
			}
		}
		
		// Example: Triggered by collision
		private void OnCollisionEnter(Collision collision)
		{
			if (collision.gameObject.CompareTag("Enemy"))
			{
				// Correct way to set trigger with recording:
				animator.SetTriggerRecorded("Hit");
			}
		}
		
		// Example: Multiple triggers in sequence
		public void PerformComboAttack()
		{
			// All triggers will be recorded properly
			animator.SetTriggerRecorded("AttackStart");
			
			// Later in the animation or code:
			Invoke(nameof(ContinueCombo), 0.5f);
		}
		
		private void ContinueCombo()
		{
			animator.SetTriggerRecorded("AttackContinue");
		}
	}
}
#endif
