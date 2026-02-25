#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
#if REMEMBERME_ADDRESSABLES_PRESENT
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
#endif

namespace Arawn.CrystalSave.Runtime
{
    public static class AssetProvider
    {
        public static bool UseAddressables { get; set; }

#if REMEMBERME_ADDRESSABLES_PRESENT
        static bool _initialized = false;
#endif

        public static T Load<T>(string key) where T : Object
        {
            var overrideAsset = CrystalSaveOverrides.GetOverride<T>();
            if (overrideAsset != null)
            {
                return overrideAsset;
            }
#if REMEMBERME_ADDRESSABLES_PRESENT
            if (UseAddressables)
            {
                T addressableAsset = LoadAddressable<T>(key);
                if (addressableAsset != null)
                {
                    return addressableAsset;
                }
            }

            T resourceAsset = Resources.Load<T>(key);
            if (resourceAsset != null)
            {
                return resourceAsset;
            }

            if (!UseAddressables)
            {
                return LoadAddressable<T>(key);
            }

            return null;
#else
            return Resources.Load<T>(key);
#endif
        }

#if REMEMBERME_ADDRESSABLES_PRESENT
        /// <summary>
        /// Loads an Addressable asset for the given key, allowing custom addresses to override
        /// the lookup while falling back to the default path when no override is found.
        /// </summary>
        /// <typeparam name="T">The Unity object type to load.</typeparam>
        /// <param name="key">The asset address or key.</param>
        /// <returns>The first successfully loaded asset instance, or <c>null</c> if none could be loaded.</returns>
        private static T LoadAddressable<T>(string key) where T : Object
        {
            EnsureAddressablesInitialized();

            if (TryLoadAddressableAsset(key, out T asset))
            {
                return asset;
            }

            if (!string.IsNullOrEmpty(key))
            {
                string fallbackKey = $"Assets/{key}.asset";
                if (TryLoadAddressableAsset(fallbackKey, out asset))
                {
                    return asset;
                }
            }

            Logger.Log($"No Addressables entry for '{key}'. Mark the asset as Addressable, set its Address field to the key, and rebuild the Addressables catalog.", LogLevel.Error);
            return null;
        }

        private static bool TryLoadAddressableAsset<T>(string key, out T asset) where T : Object
        {
            asset = null;

            if (!HasResourceLocation<T>(key))
            {
                return false;
            }

            try
            {
                asset = Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
                return asset != null;
            }
            catch (UnityEngine.AddressableAssets.InvalidKeyException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasResourceLocation<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                if (locator != null && locator.Locate(key, typeof(T), out IList<IResourceLocation> locations) &&
                    locations != null && locations.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureAddressablesInitialized()
        {
            if (_initialized)
            {
                return;
            }

            Addressables.InitializeAsync().WaitForCompletion();
            _initialized = true;
        }
#endif
    }
}
#endif
