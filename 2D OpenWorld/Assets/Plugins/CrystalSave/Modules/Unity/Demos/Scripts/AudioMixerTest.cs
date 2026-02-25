using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

namespace Arawn.CrystalSave.Demo
{
	public class AudioMixerTest : MonoBehaviour
	{
		[Header("Audio Mixer")]
		[SerializeField] private AudioMixer audioMixer;

		// Exposed parameter names (must match the names in the Audio Mixer)
		[SerializeField] private string MasterVolumeParam = "Master";
		[SerializeField] private string MusicVolumeParam = "Music";
		[SerializeField] private string SfxVolumeParam = "SFX";
		[SerializeField] private string VoiceVolumeParam = "Voice";

		private void Start()
		{
			if (audioMixer == null)
			{
				Debug.LogWarning("AudioMixerTest: No AudioMixer assigned. Please assign an AudioMixer in the Inspector.");
				return;
			}

			// Set all volumes to -80.00 dB (effectively mute) with different delays as examples
			SetVolume(MasterVolumeParam, -80f, 0f);
			SetVolume(MusicVolumeParam, -80f, 0f);
			SetVolume(SfxVolumeParam, -80f, 0f);
			SetVolume(VoiceVolumeParam, -80f, 0f);

			Debug.Log("AudioMixerTest: All volumes set to -80.00 dB (muted) with varying delays.");
		}

		/// <summary>
		/// Sets the volume of a specific Audio Mixer group with an optional delay.
		/// </summary>
		/// <param name="parameterName">The exposed parameter name (e.g., "MasterVolume").</param>
		/// <param name="volumeDb">The volume in decibels (dB).</param>
		/// <param name="delay">Optional delay in seconds before applying the volume change (default is 0).</param>
		private void SetVolume(string parameterName, float volumeDb, float delay = 0f)
		{
			StartCoroutine(SetVolumeWithDelay(parameterName, volumeDb, delay));
		}

		private IEnumerator SetVolumeWithDelay(string parameterName, float volumeDb, float delay)
		{
			if (delay > 0f)
			{
				Debug.Log($"AudioMixerTest: Waiting {delay} seconds before setting {parameterName} to {volumeDb} dB.");
				yield return new WaitForSeconds(delay);
			}

			if (audioMixer.SetFloat(parameterName, volumeDb))
			{
				Debug.Log($"AudioMixerTest: Set {parameterName} to {volumeDb} dB after {delay} second delay.");
			}
			else
			{
				Debug.LogWarning($"AudioMixerTest: Failed to set {parameterName}. Check if the parameter name is correct.");
			}
		}
	}
}
