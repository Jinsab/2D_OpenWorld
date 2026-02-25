#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Demonstrates that a RememberGameObject's active state is restored
    /// after switching away from its scene and returning.
    /// </summary>
    public class ActiveStateRestorationDemo : MonoBehaviour
    {
        [SerializeField] private RememberGameObject target;
        [SerializeField] private string otherSceneName;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            if (target == null)
            {
                Debug.LogWarning("ActiveStateRestorationDemo: target not assigned.");
                yield break;
            }

            while (!SaveManager.IsInitialized)
                yield return null;

            string originalScene = SceneManager.GetActiveScene().name;

            target.gameObject.SetActive(false);
            SaveManager.Instance.SnapshotCurrentData(originalScene);

            yield return SceneManager.LoadSceneAsync(otherSceneName);
            yield return SceneManager.LoadSceneAsync(originalScene);

            Debug.Log($"ActiveStateRestorationDemo: target active? {target.gameObject.activeSelf}");
        }
    }
}
#endif
