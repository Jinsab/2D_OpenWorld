#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public static class ResourceLoader
        {
                // Subfolder variations for each asset type
                public static readonly string[] MaterialSubfolders =
                {
                        "", "Materials", "Mats", "Mat", "materials", "mats", "mat"
                };
                public static readonly string[] TextureSubfolders =
                {
                        "", "Textures", "Tex", "Texture", "textures", "tex", "texture"
                };
                public static readonly string[] AudioSubfolders =
                {
                        "", "Audio", "audioClips", "sfx", "Sfx", "audio", "clips", "Clips", "Sound", "sound", "Audio Clips", "audio clips", "Audioclips"
                };
                public static readonly string[] AnimationSubfolders =
                {
                        "", "Animations", "anim", "Anim", "anims", "Anims", "Animation", "animation"
                };
                public static readonly string[] SpriteSubfolders =
                {
                        "", "Sprites", "Sprite", "sprites", "sprite", "Images", "images", "Icons", "icons", "Textures", "Tex", "Texture", "textures", "tex", "texture"
                };

		// Caches for each asset type
		private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
		private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();
		private static readonly Dictionary<string, AudioClip> AudioClipCache = new Dictionary<string, AudioClip>();
		private static readonly Dictionary<string, AnimationClip> AnimationClipCache = new Dictionary<string, AnimationClip>();
		private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

		// Generic loader that tries all subfolders
                private static T TryLoadFromSubfolders<T>(string assetName, IEnumerable<string> subfolders, Dictionary<string, T> cache) where T : UnityEngine.Object
                {
			if (string.IsNullOrEmpty(assetName))
			{
				Logger.Log($"ResourceLoader: assetName is null or empty for type {typeof(T).Name}.", LogLevel.Warning);
				return null;
			}

			if (cache.TryGetValue(assetName, out T cached))
			{
				return cached;
			}

                foreach (var sub in subfolders)
                {
                        string path = string.IsNullOrEmpty(sub) ? assetName : $"{sub}/{assetName}";
                        T asset = AssetProvider.Load<T>(path);
                        if (asset != null)
                        {
                                cache[assetName] = asset;
                                Logger.Log($"ResourceLoader: Found '{assetName}' in '{path}'.", LogLevel.Info);
                                return asset;
                        }
                }

                        Logger.Log($"ResourceLoader: Could not find '{assetName}' in any known subfolder for type {typeof(T).Name}.", LogLevel.Warning);
                        cache[assetName] = null;
                        return null;
                }

		public static Material TryLoadMaterialByName(string name)
		{
			return TryLoadFromSubfolders(name, MaterialSubfolders, MaterialCache);
		}

		public static Texture2D TryLoadTextureByName(string name)
		{
			return TryLoadFromSubfolders(name, TextureSubfolders, TextureCache);
		}

		public static AudioClip TryLoadAudioClipByName(string name)
		{
			return TryLoadFromSubfolders(name, AudioSubfolders, AudioClipCache);
		}

		public static AnimationClip TryLoadAnimationClipByName(string name)
		{
			return TryLoadFromSubfolders(name, AnimationSubfolders, AnimationClipCache);
		}

                public static Sprite TryLoadSpriteByName(string name)
                {
                        return TryLoadFromSubfolders(name, SpriteSubfolders, SpriteCache);
                }
        }
}
#endif
