using UnityEngine;

namespace Arawn.CrystalSave.Demo
{
	public class InstantiatePrefabInFrustum : MonoBehaviour
	{
		// Reference to the prefab, set this in the Inspector
		public GameObject prefab;

		// Reference to the main camera
		private Camera mainCamera;

		// Number of instances to spawn
		public int minInstances = 5;
		public int maxInstances = 30;

		// Speed range for movement and rotation
		public float minMoveSpeed = 1f;
		public float maxMoveSpeed = 5f;
		public float minRotationSpeed = 10f;
		public float maxRotationSpeed = 50f;

		// Maximum distance from the camera
		public float maxDistanceFromCamera = 50f;

		void Start()
		{
			// Get the main camera
			mainCamera = Camera.main;

			// Check if the prefab is assigned
			if (prefab == null)
			{
				Debug.LogError("Prefab not assigned in the Inspector!");
				return;
			}

			// Determine the number of instances to spawn
			int instanceCount = Random.Range(minInstances, maxInstances + 1);

			for (int i = 0; i < instanceCount; i++)
			{
				// Generate a random position within the camera frustum
				Vector3 randomPosition = GetRandomPositionInFrustum();

				// Generate a random rotation
				Quaternion randomRotation = Random.rotation;

				// Instantiate the prefab at the random position and rotation
				GameObject instance = Instantiate(prefab, randomPosition, randomRotation);

				// Add a movement and rotation component to the instantiated object
				RandomMover mover = instance.AddComponent<RandomMover>();
				mover.Initialize(mainCamera, minMoveSpeed, maxMoveSpeed, minRotationSpeed, maxRotationSpeed, maxDistanceFromCamera);
			}
		}

		Vector3 GetRandomPositionInFrustum()
		{
			// Get a random point in the viewport (x and y between 0 and 1)
			float randomX = Random.Range(0f, 1f);
			float randomY = Random.Range(0f, 1f);

			// Set a random distance from the camera (z-axis, in front of the camera)
			float randomDistance = Random.Range(5f, Mathf.Min(20f, maxDistanceFromCamera));

			// Convert the viewport point to a world point within the frustum
			Vector3 viewportPoint = new Vector3(randomX, randomY, randomDistance);
			return mainCamera.ViewportToWorldPoint(viewportPoint);
		}
	}

	public class RandomMover : MonoBehaviour
	{
		private Camera mainCamera;
		private float moveSpeed;
		private float rotationSpeed;
		private float maxDistanceFromCamera;

		private Vector3 targetDirection;
		private Quaternion targetRotation;

		private float changeInterval = 2f; // Time interval for changing direction/rotation
		private float changeTimer;

		public void Initialize(Camera camera, float minMoveSpeed, float maxMoveSpeed, float minRotationSpeed, float maxRotationSpeed, float maxDistance)
		{
			mainCamera = camera;
			moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
			rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
			maxDistanceFromCamera = maxDistance;

			// Set initial target direction and rotation
			SetNewTarget();
		}

		void Update()
		{
			// Move towards the target direction
			transform.position += targetDirection * moveSpeed * Time.deltaTime;

			// Smoothly rotate towards the target rotation
			transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

			// Update the timer and change direction/rotation if necessary
			changeTimer -= Time.deltaTime;
			if (changeTimer <= 0f || !IsInFrustum() || IsBeyondMaxDistance())
			{
				SetNewTarget();
			}
		}

		void SetNewTarget()
		{
			// Set a new random direction
			targetDirection = Random.insideUnitSphere;
			targetDirection.z = Mathf.Abs(targetDirection.z); // Ensure movement stays in front of the camera

			// Set a new random rotation
			targetRotation = Random.rotation;

			// Reset the timer
			changeTimer = changeInterval;
		}

		bool IsInFrustum()
		{
			Vector3 viewportPosition = mainCamera.WorldToViewportPoint(transform.position);
			return viewportPosition.x >= 0f && viewportPosition.x <= 1f && viewportPosition.y >= 0f && viewportPosition.y <= 1f && viewportPosition.z >= 0f;
		}

		bool IsBeyondMaxDistance()
		{
			return Vector3.Distance(mainCamera.transform.position, transform.position) > maxDistanceFromCamera;
		}
	}
}

