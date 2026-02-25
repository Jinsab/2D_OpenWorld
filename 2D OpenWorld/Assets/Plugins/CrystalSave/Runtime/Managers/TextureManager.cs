#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public static class TextureManager
        {
		// Dictionary to map texture names to Texture instances
		private static Dictionary<string, Texture> textureDictionary = new Dictionary<string, Texture>();

		// Initialize the TextureManager (e.g., during game initialization)
                public static void Initialize()
                {
                        textureDictionary.Clear();

                        if (!AssetProvider.UseAddressables)
                        {
                                // Only load textures from the dedicated CrystalSave texture folder.
                                // Loading from the root of Resources ("" path) causes Unity to attempt
                                // to deserialize every resource in the project, including editor-only
                                // assets from other packages (e.g. YarnSpinner's ProjectImportData),
                                // which results in missing-script or layout mismatch warnings in builds.
                                const string textureResourcePath = "CrystalSave/Textures";

                                Texture[] allTextures = Resources.LoadAll<Texture>(textureResourcePath);
                                foreach (var texture in allTextures)
                                {
                                        if (!textureDictionary.ContainsKey(texture.name))
                                        {
                                                textureDictionary.Add(texture.name, texture);
                                }
                        }

                                Logger.Log($"TextureManager: Loaded {textureDictionary.Count} textures from Resources.", LogCategory.TextureManager, LogLevel.Off);
                        }
                }		// Retrieve a texture by name
        public static Texture GetTextureByName(string name)
        {
                        if (textureDictionary.TryGetValue(name, out Texture texture))
                        {
                                return texture;
                        }

                        texture = AssetProvider.Load<Texture>(name);
                        if (texture != null)
                        {
                                textureDictionary[name] = texture;
                                return texture;
                        }

                        Logger.Log($"TextureManager: Texture with name '{name}' not found.", LogCategory.TextureManager, LogLevel.Warning);
                        return null;
                }
        }
}
#endif
