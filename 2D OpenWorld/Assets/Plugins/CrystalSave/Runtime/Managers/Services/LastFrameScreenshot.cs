#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Keeps a recent end-of-frame screenshot encoded as PNG/JPG bytes so synchronous
    /// save paths can write a recent frame without waiting for end-of-frame.
    /// This runs a low-frequency capture loop to avoid per-frame encoding costs.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LastFrameScreenshot : MonoBehaviour
    {
        public static LastFrameScreenshot Instance { get; private set; }

        // How often (seconds) to refresh the cached screenshot. Lower = fresher, higher = cheaper.
        [Tooltip("Seconds between automatic cached screenshots. Lower is fresher but costs more CPU.")]
        public float captureInterval = 0.25f;

        // Preferred format for cached screenshots
        public ScreenshotFormat format = ScreenshotFormat.PNG;

        // Cached image bytes (thread-safe access via simple locking)
        private byte[] cachedBytes;
        private string cachedExtension = "png";
        private readonly object cacheLock = new object();

        private float lastCaptureTime = -999f;
        private Coroutine captureLoop;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(this.gameObject);
        }

        private void OnEnable()
        {
            // Only run the capture loop during Play Mode
            if (Application.isPlaying)
                captureLoop = StartCoroutine(CaptureLoop());
        }

        private void OnDisable()
        {
            if (captureLoop != null) StopCoroutine(captureLoop);
            captureLoop = null;
        }

        private void OnApplicationQuit()
        {
            // Ensure the loop is stopped before Play Mode fully exits
            if (captureLoop != null)
            {
                StopCoroutine(captureLoop);
                captureLoop = null;
            }
        }

        private IEnumerator CaptureLoop()
        {
            while (true)
            {
                // If Play Mode ended between frames, exit the loop
                if (!Application.isPlaying)
                    yield break;

                yield return new WaitForEndOfFrame();

                // Abort if the application is no longer playing (e.g. exiting Play Mode in the Editor)
                if (!Application.isPlaying) yield break;

                float now = Time.realtimeSinceStartup;
                if (now - lastCaptureTime < captureInterval) continue;

                lastCaptureTime = now;
                try
                {
                    // Double-check right before capturing to avoid race conditions
                    if (!Application.isPlaying)
                        yield break;

                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    if (tex != null)
                    {
                        byte[] bytes = format == ScreenshotFormat.PNG ? tex.EncodeToPNG() : tex.EncodeToJPG();
                        lock (cacheLock)
                        {
                            cachedBytes = bytes;
                            cachedExtension = format == ScreenshotFormat.PNG ? "png" : "jpg";
                        }
                        UnityEngine.Object.Destroy(tex);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"LastFrameScreenshot: failed to capture frame: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Try to obtain the most recent cached screenshot bytes and extension.
        /// Returns false if no cached image available.
        /// </summary>
        public bool TryGetCachedScreenshot(out byte[] bytes, out string extension)
        {
            lock (cacheLock)
            {
                if (cachedBytes == null || cachedBytes.Length == 0)
                {
                    bytes = null; extension = null;
                    return false;
                }
                bytes = (byte[])cachedBytes.Clone();
                extension = cachedExtension;
                return true;
            }
        }

        /// <summary>
        /// Ensure an instance exists in the scene and return it.
        /// </summary>
        public static LastFrameScreenshot EnsureInstance(float interval = 0.25f, ScreenshotFormat fmt = ScreenshotFormat.PNG)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("LastFrameScreenshot");
            var inst = go.AddComponent<LastFrameScreenshot>();
            inst.captureInterval = interval;
            inst.format = fmt;
            Instance = inst;
            return inst;
        }
    }
}
#endif
