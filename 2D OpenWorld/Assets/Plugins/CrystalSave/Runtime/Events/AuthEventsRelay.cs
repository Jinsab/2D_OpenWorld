#if REMEMBERME_CORESERVICES_PRESENT && REMEMBERME_AUTHENTICATION_PRESENT
using System;
using UnityEngine;
using UnityEngine.Events;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Relays Unity-Authentication callbacks project-wide.
	/// Auto-spawns exactly once and survives scene changes.
	/// </summary>
	public sealed class AuthEventsRelay : MonoBehaviour 
	{
		/* ──────────────────  Singleton  ────────────────── */
		public static AuthEventsRelay Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void EnsureInstance()
		{
			if (Instance != null) return;                        // already in play
			var go = new GameObject(nameof(AuthEventsRelay));
			go.AddComponent<AuthEventsRelay>();                  // Awake() will set Instance
		}

		/* ──────────────────  Public API  ────────────────── */
		public readonly struct SignedInArgs
		{
			public readonly string Provider;
			public readonly string PlayerId;
			public SignedInArgs(string provider, string playerId)
			{ Provider = provider; PlayerId = playerId; }
		}

		public static event Action<SignedInArgs> PlayerSignedIn;

		[Serializable] public class UnitySignedInEvent : UnityEvent<SignedInArgs> { }
		[Header("Optional Inspector Event")]
		public UnitySignedInEvent OnPlayerSignedIn;

		/* ──────────────────  Bootstrap  ────────────────── */
		async void Awake()
		{
			/* Singleton enforcement */
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);                // duplicate → nuke
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);

			/* Initialise Services (if not already done by SaveManager) */
			await UnityServices.InitializeAsync();

			/* Subscribe once */
			AuthenticationService.Instance.SignedIn += OnSignedIn;
			AuthenticationService.Instance.SignedOut += OnSignedOut;
			AuthenticationService.Instance.SignInFailed += OnSignInFailed;

			/* Catch the sign-in that SaveManager may have finished already */
			if (AuthenticationService.Instance.IsSignedIn)
				OnSignedIn();
		}

		/* ──────────────────  Callbacks  ────────────────── */
		void OnSignedIn()
		{
			var ids = AuthenticationService.Instance.PlayerInfo?.Identities;
			string p = (ids != null && ids.Count > 0) ? ids[0].TypeId : "Anonymous/unknown";
			string id = AuthenticationService.Instance.PlayerId;

			Debug.Log($"[Auth] Player signed in via {p}. PlayerId = {id}");

			var args = new SignedInArgs(p, id);
			PlayerSignedIn?.Invoke(args);
			OnPlayerSignedIn?.Invoke(args);
		}

		void OnSignedOut() =>
			Debug.Log("[Auth] Player signed out.");

		void OnSignInFailed(RequestFailedException ex) =>
			Debug.LogError($"[Auth] Sign-in failed: {ex}");

		/* ──────────────────  Cleanup  ────────────────── */
		void OnDestroy()
		{
			if (Instance == this) Instance = null;

			if (!UnityServices.State.Equals(ServicesInitializationState.Initialized)) return;

			AuthenticationService.Instance.SignedIn -= OnSignedIn;
			AuthenticationService.Instance.SignedOut -= OnSignedOut;
			AuthenticationService.Instance.SignInFailed -= OnSignInFailed;
		}
	}
}
#endif
