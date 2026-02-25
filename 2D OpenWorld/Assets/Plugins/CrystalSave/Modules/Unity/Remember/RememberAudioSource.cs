#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember AudioSource")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RememberTarget(typeof(AudioSource))]
    public class RememberAudioSource : SaveableComponent
    {
        [Header("Save Optimization")]
        [Tooltip("Skip saving when the AudioSource data has not changed since the last save.")]
        [SerializeField] private bool skipSavingWhenUnchanged = false;

        private AudioSource _audioSource;
        private RememberAudioSourceData _cachedSnapshot;
        private bool _cachedSnapshotCaptured;
        private byte[] cachedSerializedData;

        protected override void Awake()
        {
            base.Awake();
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                Logger.Log($"{nameof(RememberAudioSource)} requires an AudioSource component on '{gameObject.name}'.", LogCategory.RememberAudioSource, LogLevel.Error);
                enabled = false;
                return;
            }

            if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
            {
                CacheSnapshot(snapshot);
            }
        }

        protected override byte[] SerializeComponentData()
        {
            if (_audioSource == null)
            {
                Logger.Log("SerializeComponentData failed: AudioSource component not found.", LogCategory.RememberAudioSource, LogLevel.Warning);
                return Array.Empty<byte>();
            }

            if (!TryCaptureCurrentState(out var snapshot))
            {
                return null;
            }

            if (skipSavingWhenUnchanged && _cachedSnapshotCaptured && _cachedSnapshot != null)
            {
                if (AreEquivalent(_cachedSnapshot, snapshot))
                {
                    if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                    {
                        return cachedSerializedData;
                    }
                }
            }

            byte[] serialized = SaveDataSerializer.Instance.Serialize(snapshot);

            if (skipSavingWhenUnchanged)
            {
                CacheSnapshot(snapshot);
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        protected override void DeserializeComponentData(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Logger.Log("DeserializeComponentData failed: Data is null or empty.", LogCategory.RememberAudioSource, LogLevel.Warning);
                return;
            }
            if (_audioSource == null)
            {
                Logger.Log("DeserializeComponentData failed: AudioSource component not found.", LogCategory.RememberAudioSource, LogLevel.Warning);
                return;
            }

            try
            {
                var data = SaveDataSerializer.Instance.Deserialize<RememberAudioSourceData>(bytes);
                if (data == null)
                {
                    Logger.Log("Deserialized data is null.", LogCategory.RememberAudioSource, LogLevel.Warning);
                    return;
                }

                // Check if we're in TimeMachine playback mode
#if CRYSTALSAVE_TIMEMACHINE
                bool isTimeMachinePlayback = TimeMachine.TimeMachinePlaybackContext.HasSample;
#else
                bool isTimeMachinePlayback = false;
#endif

                _audioSource.bypassEffects = data.BypassEffects;
                _audioSource.bypassListenerEffects = data.BypassListenerEffects;
                _audioSource.bypassReverbZones = data.BypassReverbZones;
                _audioSource.playOnAwake = data.PlayOnAwake;
                _audioSource.loop = data.Loop;
                _audioSource.mute = data.Mute;
                _audioSource.spatialize = data.Spatialize;
                _audioSource.ignoreListenerPause = data.IgnoreListenerPause;
                _audioSource.ignoreListenerVolume = data.IgnoreListenerVolume;

                _audioSource.dopplerLevel = data.DopplerLevel;
                _audioSource.spread = data.Spread;
                _audioSource.minDistance = data.MinDistance;
                _audioSource.maxDistance = data.MaxDistance;
                _audioSource.volume = data.Volume;
                
                // During TimeMachine playback, DON'T restore pitch - let TimeMachine control it for reverse playback
                if (!isTimeMachinePlayback)
                {
                    _audioSource.pitch = data.Pitch;
                }
                
                _audioSource.panStereo = data.StereoPan;
                _audioSource.spatialBlend = data.SpatialBlend;
                _audioSource.reverbZoneMix = data.ReverbZoneMix;
                _audioSource.priority = data.Priority;

                _audioSource.rolloffMode = data.RolloffMode;

                if (data.Clip != null)
                {
                    if (data.Clip.GetValue() is AudioClip clip)
                        _audioSource.clip = clip;
                }
                else
                {
                    _audioSource.clip = null;
                }

                if (!string.IsNullOrEmpty(data.OutputMixerName))
                {
                    AudioMixer mixer = AssetProvider.Load<AudioMixer>(data.OutputMixerName);
                    if (mixer != null)
                    {
                        var groups = mixer.FindMatchingGroups(data.OutputGroupName ?? string.Empty);
                        if (groups != null && groups.Length > 0)
                            _audioSource.outputAudioMixerGroup = groups[0];
                        else
                            Logger.Log($"RememberAudioSource: Could not find AudioMixerGroup '{data.OutputGroupName}' in mixer '{data.OutputMixerName}'.", LogCategory.RememberAudioSource, LogLevel.Warning);
                    }
                    else
                    {
                        Logger.Log($"RememberAudioSource: Could not load AudioMixer '{data.OutputMixerName}' from Resources.", LogCategory.RememberAudioSource, LogLevel.Warning);
                    }
                }
                else
                {
                    _audioSource.outputAudioMixerGroup = null;
                }

                // During TimeMachine playback, use snapshot only for audio time position (scrubbing)
                // Don't control play/pause state - let TimeMachine handle it
                if (isTimeMachinePlayback)
                {
                    // During reverse playback, DON'T set audioSource.time from snapshots
                    // Let the AudioSource play naturally in reverse via negative pitch
#if CRYSTALSAVE_TIMEMACHINE
                    bool isReversePlayback = TimeMachine.TimeMachinePlaybackContext.CurrentSample.PlaybackSpeed < 0f;
#else
                    bool isReversePlayback = false;
#endif
                    
                    if (_audioSource.clip != null && data.IsPlaying && !isReversePlayback)
                    {
                        // Only sync time during forward playback
                        _audioSource.time = data.PlaybackTime;
                    }
                    // During reverse playback, time advances naturally via negative pitch - don't interfere!
                }
                else
                {
                    // Normal save/load: restore full playback state
                    if (data.IsPlaying && _audioSource.clip != null)
                    {
                        _audioSource.time = data.PlaybackTime;
                        _audioSource.Play();
                    }
                }

                if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var refreshedSnapshot))
                {
                    CacheSnapshot(refreshedSnapshot);
                }
                else if (skipSavingWhenUnchanged)
                {
                    _cachedSnapshotCaptured = false;
                    _cachedSnapshot = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Deserialization failed: {ex.Message}", LogCategory.RememberAudioSource, LogLevel.Error);
            }
        }

        private bool TryCaptureCurrentState(out RememberAudioSourceData snapshot)
        {
            snapshot = null;

            if (_audioSource == null)
            {
                return false;
            }

            snapshot = new RememberAudioSourceData
            {
                BypassEffects = _audioSource.bypassEffects,
                BypassListenerEffects = _audioSource.bypassListenerEffects,
                BypassReverbZones = _audioSource.bypassReverbZones,
                PlayOnAwake = _audioSource.playOnAwake,
                Loop = _audioSource.loop,
                Mute = _audioSource.mute,
                Spatialize = _audioSource.spatialize,
                IgnoreListenerPause = _audioSource.ignoreListenerPause,
                IgnoreListenerVolume = _audioSource.ignoreListenerVolume,

                DopplerLevel = _audioSource.dopplerLevel,
                Spread = _audioSource.spread,
                MinDistance = _audioSource.minDistance,
                MaxDistance = _audioSource.maxDistance,
                Volume = _audioSource.volume,
                Pitch = _audioSource.pitch,
                StereoPan = _audioSource.panStereo,
                SpatialBlend = _audioSource.spatialBlend,
                ReverbZoneMix = _audioSource.reverbZoneMix,
                Priority = _audioSource.priority,

                RolloffMode = _audioSource.rolloffMode,
                Clip = _audioSource.clip != null ? new AudioClipWrapper(_audioSource.clip) : null,
                OutputGroupName = _audioSource.outputAudioMixerGroup ? _audioSource.outputAudioMixerGroup.name : null,
                OutputMixerName = _audioSource.outputAudioMixerGroup ? _audioSource.outputAudioMixerGroup.audioMixer.name : null,
                IsPlaying = _audioSource.isPlaying,
                PlaybackTime = _audioSource.time
            };

            return true;
        }

        private void CacheSnapshot(RememberAudioSourceData snapshot)
        {
            if (snapshot == null)
            {
                _cachedSnapshotCaptured = false;
                _cachedSnapshot = null;
                return;
            }

            _cachedSnapshot = CloneSnapshot(snapshot);
            _cachedSnapshotCaptured = true;
        }

        private RememberAudioSourceData CloneSnapshot(RememberAudioSourceData source)
        {
            if (source == null)
            {
                return null;
            }

            return new RememberAudioSourceData
            {
                BypassEffects = source.BypassEffects,
                BypassListenerEffects = source.BypassListenerEffects,
                BypassReverbZones = source.BypassReverbZones,
                PlayOnAwake = source.PlayOnAwake,
                Loop = source.Loop,
                Mute = source.Mute,
                Spatialize = source.Spatialize,
                IgnoreListenerPause = source.IgnoreListenerPause,
                IgnoreListenerVolume = source.IgnoreListenerVolume,

                DopplerLevel = source.DopplerLevel,
                Spread = source.Spread,
                MinDistance = source.MinDistance,
                MaxDistance = source.MaxDistance,
                Volume = source.Volume,
                Pitch = source.Pitch,
                StereoPan = source.StereoPan,
                SpatialBlend = source.SpatialBlend,
                ReverbZoneMix = source.ReverbZoneMix,
                Priority = source.Priority,

                RolloffMode = source.RolloffMode,
                Clip = source.Clip != null ? new AudioClipWrapper { ClipName = source.Clip.ClipName } : null,
                OutputMixerName = source.OutputMixerName,
                OutputGroupName = source.OutputGroupName,
                IsPlaying = source.IsPlaying,
                PlaybackTime = source.PlaybackTime
            };
        }

        private bool AreEquivalent(RememberAudioSourceData baseline, RememberAudioSourceData snapshot)
        {
            if (baseline == null || snapshot == null)
            {
                return false;
            }

            if (baseline.BypassEffects != snapshot.BypassEffects ||
                baseline.BypassListenerEffects != snapshot.BypassListenerEffects ||
                baseline.BypassReverbZones != snapshot.BypassReverbZones ||
                baseline.PlayOnAwake != snapshot.PlayOnAwake ||
                baseline.Loop != snapshot.Loop ||
                baseline.Mute != snapshot.Mute ||
                baseline.Spatialize != snapshot.Spatialize ||
                baseline.IgnoreListenerPause != snapshot.IgnoreListenerPause ||
                baseline.IgnoreListenerVolume != snapshot.IgnoreListenerVolume)
            {
                return false;
            }

            if (!Mathf.Approximately(baseline.DopplerLevel, snapshot.DopplerLevel) ||
                !Mathf.Approximately(baseline.Spread, snapshot.Spread) ||
                !Mathf.Approximately(baseline.MinDistance, snapshot.MinDistance) ||
                !Mathf.Approximately(baseline.MaxDistance, snapshot.MaxDistance) ||
                !Mathf.Approximately(baseline.Volume, snapshot.Volume) ||
                !Mathf.Approximately(baseline.Pitch, snapshot.Pitch) ||
                !Mathf.Approximately(baseline.StereoPan, snapshot.StereoPan) ||
                !Mathf.Approximately(baseline.SpatialBlend, snapshot.SpatialBlend) ||
                !Mathf.Approximately(baseline.ReverbZoneMix, snapshot.ReverbZoneMix))
            {
                return false;
            }

            if (baseline.Priority != snapshot.Priority)
            {
                return false;
            }

            if (baseline.RolloffMode != snapshot.RolloffMode)
            {
                return false;
            }

            string baselineClipName = baseline.Clip?.ClipName;
            string snapshotClipName = snapshot.Clip?.ClipName;
            if (!string.Equals(baselineClipName, snapshotClipName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(baseline.OutputMixerName, snapshot.OutputMixerName, StringComparison.Ordinal) ||
                !string.Equals(baseline.OutputGroupName, snapshot.OutputGroupName, StringComparison.Ordinal))
            {
                return false;
            }

            if (baseline.IsPlaying != snapshot.IsPlaying)
            {
                return false;
            }

            if (!Mathf.Approximately(baseline.PlaybackTime, snapshot.PlaybackTime))
            {
                return false;
            }

            return true;
        }
    }

    [MemoryPackable]
    public partial class RememberAudioSourceData
    {
        public bool BypassEffects { get; set; }
        public bool BypassListenerEffects { get; set; }
        public bool BypassReverbZones { get; set; }
        public bool PlayOnAwake { get; set; }
        public bool Loop { get; set; }
        public bool Mute { get; set; }
        public bool Spatialize { get; set; }
        public bool IgnoreListenerPause { get; set; }
        public bool IgnoreListenerVolume { get; set; }

        public float DopplerLevel { get; set; }
        public float Spread { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float StereoPan { get; set; }
        public float SpatialBlend { get; set; }
        public float ReverbZoneMix { get; set; }
        public int Priority { get; set; }

        public AudioRolloffMode RolloffMode { get; set; }
        public AudioClipWrapper Clip { get; set; }
        public string OutputMixerName { get; set; }
        public string OutputGroupName { get; set; }
        public bool IsPlaying { get; set; }
        public float PlaybackTime { get; set; }

        public RememberAudioSourceData() { }
    }
}
#endif
