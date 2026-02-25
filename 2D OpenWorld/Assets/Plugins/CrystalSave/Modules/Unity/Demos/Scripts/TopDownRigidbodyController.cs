using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// A simple top-down movement controller that drives a kinematic Rigidbody.
    /// Uses the new Input System just like <see cref="TopDownCharacterController"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TopDownRigidbodyController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Units per second on the XZ plane.")]
        public float moveSpeed = 5f;

        [Tooltip("How fast the Rigidbody accelerates toward the target velocity.")]
        public float acceleration = 20f;

        [Tooltip("How fast the Rigidbody decelerates when no movement input is held.")]
        public float deceleration = 25f;

        private Rigidbody body;
        private Vector2 moveInput;
        private Vector3 horizontalVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
        }

        private void Update()
        {
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;

            moveInput = input.normalized;
#else
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");

            moveInput = input.sqrMagnitude > 1f ? input.normalized : input;
#endif
        }

        private void FixedUpdate()
        {
            Vector3 targetVelocity = new Vector3(moveInput.x, 0f, moveInput.y) * moveSpeed;
            float moveRate = moveInput.sqrMagnitude > 0f ? acceleration : deceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                moveRate * Time.fixedDeltaTime
            );

            body.MovePosition(body.position + horizontalVelocity * Time.fixedDeltaTime);
        }
    }
}
