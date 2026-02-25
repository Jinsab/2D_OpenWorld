#if ARAWN_REMEMBERME && MEMORYPACK
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using UnityEngine;

#if REMEMBERME_GC2MODULE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2CORE_PRESENT && MEMORYPACK
using Arawn.CrystalSave.GameCreator2.Runtime;
#endif

#if REMEMBERME_GC2MELEE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK && REMEMBERME_GC2CORE_PRESENT
using GameCreator.Runtime.Melee;
#endif
#if REMEMBERME_GC2QUESTS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK && REMEMBERME_GC2CORE_PRESENT
using GameCreator.Runtime.Quests;
#endif
#if REMEMBERME_GC2INVENTORY_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK && REMEMBERME_GC2CORE_PRESENT
using GameCreator.Runtime.Inventory;
#endif
#if REMEMBERME_GC2STATS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK && REMEMBERME_GC2CORE_PRESENT
using GameCreator.Runtime.Stats;
#endif
#if REMEMBERME_GC2SHOOTER_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK && REMEMBERME_GC2CORE_PRESENT
using GameCreator.Runtime.Shooter;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Arawn.CrystalSave.Runtime
{ 
	[MemoryPackable]
	[MemoryPackUnion(0, typeof(IntWrapper))]
	[MemoryPackUnion(1, typeof(StringWrapper))]
	[MemoryPackUnion(2, typeof(GameObjectWrapper))]
	[MemoryPackUnion(3, typeof(BoolWrapper))]
	[MemoryPackUnion(4, typeof(DoubleWrapper))]
	[MemoryPackUnion(5, typeof(Texture2DWrapper))]
	[MemoryPackUnion(6, typeof(ColorWrapper))]
	[MemoryPackUnion(7, typeof(Vector2Wrapper))]
	[MemoryPackUnion(8, typeof(AudioClipWrapper))]
	[MemoryPackUnion(9, typeof(AnimationWrapper))]
	[MemoryPackUnion(10, typeof(SpriteWrapper))]
	[MemoryPackUnion(11, typeof(Vector3Wrapper))]
	[MemoryPackUnion(12, typeof(FloatWrapper))]
        [MemoryPackUnion(13, typeof(EnumWrapper))]
        [MemoryPackUnion(14, typeof(TransformWrapper))]
        [MemoryPackUnion(15, typeof(ListWrapper))]
        [MemoryPackUnion(16, typeof(DictionaryWrapper))]
        [MemoryPackUnion(17, typeof(TextureWrapper))]
        [MemoryPackUnion(18, typeof(MaterialWrapper))]
#if REMEMBERME_GC2MELEE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
        [MemoryPackUnion(19, typeof(MeleeWeaponWrapper))]
        [MemoryPackUnion(20, typeof(SkillWrapper))]
        [MemoryPackUnion(21, typeof(ShieldWrapper))]
#endif
#if REMEMBERME_GC2SHOOTER_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
        [MemoryPackUnion(22, typeof(ShooterWeaponWrapper))]
#endif
#if REMEMBERME_GC2QUESTS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
        [MemoryPackUnion(23, typeof(QuestWrapper))]
#endif
#if REMEMBERME_GC2INVENTORY_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
        [MemoryPackUnion(24, typeof(ItemWrapper))]
#endif
#if REMEMBERME_GC2STATS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
        [MemoryPackUnion(25, typeof(AttributeWrapper))]
        [MemoryPackUnion(26, typeof(StatWrapper))]
        [MemoryPackUnion(27, typeof(StatusEffectWrapper))]
        [MemoryPackUnion(28, typeof(FormulaWrapper))]
#endif
        public abstract partial class TypedObject
        {
                public abstract object GetValue();
        }

	[MemoryPackable]
	public partial class IntWrapper : TypedObject
	{
		public int Value { get; set; }

		[MemoryPackConstructor]
		public IntWrapper() { }

		public IntWrapper(int value)
		{
			Value = value;
		}

		public override object GetValue() => Value;
	}

        [MemoryPackable]
        public partial class StringWrapper : TypedObject
        {
                private string sanitizedValue;

                public string Value
                {
                        get => sanitizedValue;
                        set => sanitizedValue = Cleanse(value);
                }

                [MemoryPackConstructor]
                public StringWrapper() { }

                public StringWrapper(string value)
                {
                        Value = value;
                }

                public override object GetValue() => Value;

                internal static string Cleanse(string raw)
                {
                        if (string.IsNullOrEmpty(raw))
                        {
                                return raw;
                        }

                        System.Text.StringBuilder builder = null;

                        for (int i = 0; i < raw.Length; i++)
                        {
                                char current = raw[i];
                                if (IsProblematicCharacter(current))
                                {
                                        if (builder == null)
                                        {
                                                builder = new System.Text.StringBuilder(raw.Length);
                                                if (i > 0)
                                                {
                                                        builder.Append(raw, 0, i);
                                                }
                                        }

                                        continue;
                                }

                                builder?.Append(current);
                        }

                        return builder == null ? raw : builder.ToString();
                }

                private static bool IsProblematicCharacter(char c)
                {
                        if (c == '\0' || c == '\ufeff' || c == '\u200b' || c == '\u200c' || c == '\u200d' || c == '\u2060' || c == '\ufffd')
                        {
                                return true;
                        }

                        if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
                        {
                                return true;
                        }

                        if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                        {
                                return true;
                        }

                        return false;
                }
        }

	[MemoryPackable]
	public partial class GameObjectWrapper : TypedObject
	{
		public string UniqueID { get; set; }

		[MemoryPackConstructor]
		public GameObjectWrapper() { }

		public GameObjectWrapper(string uniqueID)
		{
			UniqueID = uniqueID;
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(UniqueID))
			{
				Logger.Log("GameObjectWrapper: UniqueID is null or empty. Cannot reconstruct GameObject.", LogLevel.Warning);
				return null;
			}

			if (SaveManager.Instance != null)
			{
                                var gameObject = SaveManager.Instance.FindGameObjectByUniqueID(UniqueID, SaveManager.IdentifierType.UniqueID);
				if (gameObject != null)
				{
					return gameObject;
				}
				else
				{
					Logger.Log($"GameObjectWrapper: No GameObject found with UniqueID '{UniqueID}'.", LogLevel.Warning);
				}
			}
			else
			{
				Logger.Log("GameObjectWrapper: SaveManager instance is null. Cannot find GameObject.", LogLevel.Error);
			}

			return null;
		}
	}

	[MemoryPackable]
	public partial class BoolWrapper : TypedObject
	{
		public bool Value { get; set; }

		[MemoryPackConstructor]
		public BoolWrapper() { }

		public BoolWrapper(bool value)
		{
			Value = value;
		}

		public override object GetValue() => Value;
	}

	[MemoryPackable]
	public partial class DoubleWrapper : TypedObject
	{
		public double Value { get; set; }

		[MemoryPackConstructor]
		public DoubleWrapper() { }

		public DoubleWrapper(double value)
		{
			Value = value;
		}

		public override object GetValue() => Value;
	}

	[MemoryPackable]
	public partial class Texture2DWrapper : TypedObject
	{
		public string TextureName { get; set; }

		private static readonly string[] PredefinedTextureFolders =
		{
			"", // Base Resources folder
			"textures", "Textures", "TEXTURES",
			"images", "Images", "IMAGES",
			"Tex", "tex", "SPRITES"
		};

		[MemoryPackConstructor]
		public Texture2DWrapper() { }

		public Texture2DWrapper(Texture2D texture)
		{
			if (texture == null)
			{
				Logger.Log("Texture2DWrapper: Provided Texture2D is null.", LogLevel.Warning);
				return;
			}

			TextureName = texture.name;
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(TextureName))
			{
				Logger.Log("Texture2DWrapper: TextureName is null or empty.", LogLevel.Warning);
				return null;
			}

			// Step 1: Try loading the Texture2D from predefined folders
			foreach (var folder in PredefinedTextureFolders)
			{
				string path = string.IsNullOrEmpty(folder) ? TextureName : $"{folder}/{TextureName}";

                                var texture = AssetProvider.Load<Texture2D>(path);
				if (texture != null)
				{
					Logger.Log($"Texture2DWrapper: Found Texture2D '{TextureName}' at '{path}'.", LogLevel.Info);
					return texture;
				}
			}
#if REMEMBERME_GC2CORE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			// Step 2: Fall back to using the registry if not found in predefined folders
                        Texture2DRegistrySO registry = AssetProvider.Load<Texture2DRegistrySO>("Texture2DRegistry");
			if (registry == null)
			{
				Logger.Log("Texture2DWrapper: Texture2DRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			string resourcePath = registry.GetResourcePath(TextureName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"Texture2DWrapper: Texture '{TextureName}' is not registered in the registry.", LogLevel.Warning);
				return null;
			}

                        Texture2D textureFromRegistry = AssetProvider.Load<Texture2D>(resourcePath);
			if (textureFromRegistry != null)
			{
				Logger.Log($"Texture2DWrapper: Found Texture2D '{TextureName}' in registry at '{resourcePath}'.", LogLevel.Info);
				return textureFromRegistry;
			}

			Logger.Log($"Texture2DWrapper: Could not load Texture2D '{TextureName}' from registry path '{resourcePath}'.", LogLevel.Error);
#endif
			return null;
		}
	}



	[MemoryPackable]
	public partial class ColorWrapper : TypedObject
	{
		public float R { get; set; }
		public float G { get; set; }
		public float B { get; set; }
		public float A { get; set; }

		[MemoryPackConstructor]
		public ColorWrapper() { }

		public ColorWrapper(Color color)
		{
			R = color.r;
			G = color.g;
			B = color.b;
			A = color.a;
		}

		public override object GetValue() => new Color(R, G, B, A);
	}

	[MemoryPackable]
	public partial class Vector2Wrapper : TypedObject
	{
		public float X { get; set; }
		public float Y { get; set; }

		[MemoryPackConstructor]
		public Vector2Wrapper() { }

		public Vector2Wrapper(Vector2 vector)
		{
			X = vector.x;
			Y = vector.y;
		}

		public override object GetValue() => new Vector2(X, Y);
	}

	[MemoryPackable]
	public partial class AudioClipWrapper : TypedObject
	{
		public string ClipName { get; set; }

		private static readonly string[] PredefinedAudioFolders =
		{
			"", // Base Resources folder
			"sfx", "Sfx", "SFX",
			"audio", "Audio", "AUDIO",
			"audios", "Audios", "AUDIOS",
			"sounds", "Sounds", "SOUNDS",
			"soundeffects", "SoundEffects", "SOUNDEFFECTS",
			"music", "Music", "MUSIC",
			"audioclips", "AudioClips", "AUDIOCLIPS",
			"audioclip", "AudioClip", "AUDIOCLIP"
		};

		[MemoryPackConstructor]
		public AudioClipWrapper() { }

		public AudioClipWrapper(AudioClip clip)
		{
			if (clip == null)
			{
				Logger.Log("AudioClipWrapper: Provided AudioClip is null.", LogLevel.Info);
				return;
			}

			ClipName = clip.name;
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ClipName))
			{
				Logger.Log("AudioClipWrapper: ClipName is null or empty.", LogLevel.Info);
				return null;
			}

			// Try loading the AudioClip from predefined folders
			foreach (var folder in PredefinedAudioFolders)
			{
				string path = string.IsNullOrEmpty(folder) ? ClipName : $"{folder}/{ClipName}";

                                var clip = AssetProvider.Load<AudioClip>(path);
				if (clip != null)
				{
					Logger.Log($"AudioClipWrapper: Found AudioClip '{ClipName}' at '{path}'.", LogLevel.Info);
					return clip;
				}
			}
#if REMEMBERME_GC2CORE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			// Fall back to using the registry
                        AudioClipRegistrySO registry = AssetProvider.Load<AudioClipRegistrySO>("AudioClipRegistry");
			if (registry == null)
			{
				Logger.Log("AudioClipWrapper: AudioClipRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			string resourcePath = registry.GetResourcePath(ClipName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"AudioClipWrapper: AudioClip '{ClipName}' is not registered in the registry.", LogLevel.Warning);
				return null;
			}

                        AudioClip clipFromRegistry = AssetProvider.Load<AudioClip>(resourcePath);
			if (clipFromRegistry != null)
			{
				Logger.Log($"AudioClipWrapper: Found AudioClip '{ClipName}' in registry at '{resourcePath}'.", LogLevel.Info);
				return clipFromRegistry;
			}

			Logger.Log($"AudioClipWrapper: Could not load AudioClip '{ClipName}' from registry path '{resourcePath}'.", LogLevel.Error);
#endif
			return null;

		}
	}

	[MemoryPackable]
	public partial class AnimationWrapper : TypedObject
	{
		public string AnimationClipName { get; set; }

		private static readonly string[] PredefinedAnimationFolders =
		{
			"", // Base Resources folder
			"Anim", "anim", "Anims", "anims",
			"Animation", "animation", "Animations", "animations"
		};

		[MemoryPackConstructor]
		public AnimationWrapper() { }

		public AnimationWrapper(AnimationClip animationClip)
		{
			if (animationClip == null)
			{
				Logger.Log("AnimationWrapper: Provided AnimationClip is null.");
				return;
			}

			AnimationClipName = animationClip.name;
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(AnimationClipName))
			{
				Logger.Log("AnimationWrapper: AnimationClipName is null or empty.", LogLevel.Info);
				return null;
			}

			// Try loading the AnimationClip from predefined folders
			foreach (var folder in PredefinedAnimationFolders)
			{
				string path = string.IsNullOrEmpty(folder)
					? AnimationClipName
					: $"{folder}/{AnimationClipName}";

                                var animationClip = AssetProvider.Load<AnimationClip>(path);
				if (animationClip != null)
				{
					Logger.Log($"AnimationWrapper: Found AnimationClip '{AnimationClipName}' at '{path}'.", LogLevel.Info);
					return animationClip;
				}
			}

#if REMEMBERME_GC2CORE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			// Fall back to using the registry
                        AnimationRegistrySO registry = AssetProvider.Load<AnimationRegistrySO>("AnimationRegistry");
			if (registry == null)
			{
				Logger.Log("AnimationWrapper: AnimationRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			string resourcePath = registry.GetResourcePath(AnimationClipName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"AnimationWrapper: AnimationClip '{AnimationClipName}' is not registered in the registry.", LogLevel.Warning);
				return null;
			}

                        AnimationClip clipFromRegistry = AssetProvider.Load<AnimationClip>(resourcePath);
			if (clipFromRegistry != null)
			{
				Logger.Log($"AnimationWrapper: Found AnimationClip '{AnimationClipName}' in registry at '{resourcePath}'.", LogLevel.Info);
				return clipFromRegistry;
			}

			Logger.Log($"AnimationWrapper: Could not load AnimationClip '{AnimationClipName}' from registry path '{resourcePath}'.", LogLevel.Error);
#endif
			return null;
		}
	}

	[MemoryPackable]
	public partial class SpriteWrapper : TypedObject
	{
		public string SpriteName { get; set; }

		private static readonly string[] PredefinedSpriteFolders =
		{
			"", // Base Resources folder
			"sprites", "Sprites", "SPRITES",
			"images", "Images", "IMAGES",
			"textures", "Textures", "TEXTURES",
			"graphics", "Graphics", "GRAPHICS"
		};

		[MemoryPackConstructor]
		public SpriteWrapper() { }

		public SpriteWrapper(Sprite sprite)
		{
			if (sprite == null)
			{
				Logger.Log("SpriteWrapper: Provided Sprite is null.", LogLevel.Info);
				return;
			}

			SpriteName = sprite.name;
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(SpriteName))
			{
				Logger.Log("SpriteWrapper: SpriteName is null or empty.", LogLevel.Info);
				return null;
			}

			// Try loading the Sprite from predefined folders
			foreach (var folder in PredefinedSpriteFolders)
			{
				string path = string.IsNullOrEmpty(folder) ? SpriteName : $"{folder}/{SpriteName}";

                                var sprite = AssetProvider.Load<Sprite>(path);
				if (sprite != null)
				{
					Logger.Log($"SpriteWrapper: Found Sprite '{SpriteName}' at '{path}'.", LogLevel.Info);
					return sprite;
				}
			}
#if REMEMBERME_GC2CORE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			// Fall back to using the registry
                        SpriteRegistrySO registry = AssetProvider.Load<SpriteRegistrySO>("SpriteRegistry");
			if (registry == null)
			{
				Logger.Log("SpriteWrapper: SpriteRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			string resourcePath = registry.GetResourcePath(SpriteName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"SpriteWrapper: Sprite '{SpriteName}' is not registered in the registry.", LogLevel.Warning);
				return null;
			}

                        Sprite spriteFromRegistry = AssetProvider.Load<Sprite>(resourcePath);
			if (spriteFromRegistry != null)
			{
				Logger.Log($"SpriteWrapper: Found Sprite '{SpriteName}' in registry at '{resourcePath}'.", LogLevel.Info);
				return spriteFromRegistry;
			}

			Logger.Log($"SpriteWrapper: Could not load Sprite '{SpriteName}' from registry path '{resourcePath}'.", LogLevel.Error);
#endif
			return null;
		}
	}

	[MemoryPackable]
	public partial class Vector3Wrapper : TypedObject
	{
		public float X { get; set; }
		public float Y { get; set; }
		public float Z { get; set; }

		[MemoryPackConstructor]
		public Vector3Wrapper() { }

		public Vector3Wrapper(Vector3 vector)
		{
			X = vector.x;
			Y = vector.y;
			Z = vector.z;
		}

		public override object GetValue() => new Vector3(X, Y, Z);
	}

	[MemoryPackable]
        public partial class FloatWrapper : TypedObject
        {
                public float Value { get; set; }

		[MemoryPackConstructor]
		public FloatWrapper() { }

		public FloatWrapper(float value)
		{
			Value = value;
		}

                public override object GetValue() => Value;
        }

        [MemoryPackable]
        public partial class EnumWrapper : TypedObject
        {
                public string EnumType { get; set; }
                public string EnumValue { get; set; }

                [MemoryPackConstructor]
                public EnumWrapper() { }

                public EnumWrapper(Enum value)
                {
                        if (value == null) return;
                        EnumType = value.GetType().AssemblyQualifiedName;
                        EnumValue = value.ToString();
                }

                public override object GetValue()
                {
                        if (string.IsNullOrEmpty(EnumType) || string.IsNullOrEmpty(EnumValue))
                                return null;

                        var type = Type.GetType(EnumType);
                        if (type == null || !type.IsEnum) return null;

                        try
                        {
                                return Enum.Parse(type, EnumValue);
                        }
                        catch
                        {
                                Logger.Log($"EnumWrapper: Failed to parse '{EnumValue}' for type '{EnumType}'.", LogLevel.Warning);
                                return null;
                        }
                }
        }

        [MemoryPackable]
        public partial class TransformWrapper : TypedObject
        {
                public string UniqueID { get; set; }

                [MemoryPackConstructor]
                public TransformWrapper() { }

                public TransformWrapper(Transform transform)
                {
                        if (transform == null)
                        {
                                Logger.Log("TransformWrapper: Provided Transform is null.", LogLevel.Info);
                                return;
                        }

                        UniqueID = GameObjectUtilities.GetUniqueID(transform.gameObject);
                }

                public override object GetValue()
                {
                        if (string.IsNullOrEmpty(UniqueID))
                                return null;

                        var go = SaveManager.Instance?.FindGameObjectByUniqueID(UniqueID, SaveManager.IdentifierType.UniqueID);
                        return go != null ? go.transform : null;
                }
        }

        [MemoryPackable]
        public partial class ListWrapper : TypedObject
        {
                public List<TypedObject> Items { get; set; }

                [MemoryPackConstructor]
                public ListWrapper() { }

                public ListWrapper(IList list)
                {
                        if (list == null)
                                return;

                        Items = new List<TypedObject>(list.Count);
                        foreach (var item in list)
                        {
                                Items.Add(TypedObjectFactory.CreateTypedObject(item));
                        }
                }

                public override object GetValue()
                {
                        if (Items == null)
                                return null;

                        var result = new List<object>(Items.Count);
                        foreach (var item in Items)
                        {
                                result.Add(item?.GetValue());
                        }
                        return result;
                }
        }

        [MemoryPackable]
        public partial class DictionaryWrapper : TypedObject
        {
                [MemoryPackable]
                public partial class Entry
                {
                        public TypedObject Key { get; set; }
                        public TypedObject Value { get; set; }
                }

                public List<Entry> Items { get; set; }

                [MemoryPackConstructor]
                public DictionaryWrapper() { }

                public DictionaryWrapper(IDictionary dictionary)
                {
                        if (dictionary == null)
                                return;

                        Items = new List<Entry>(dictionary.Count);
                        foreach (DictionaryEntry kvp in dictionary)
                        {
                                Items.Add(new Entry
                                {
                                        Key = TypedObjectFactory.CreateTypedObject(kvp.Key),
                                        Value = TypedObjectFactory.CreateTypedObject(kvp.Value)
                                });
                        }
                }

                public override object GetValue()
                {
                        if (Items == null)
                                return null;

                        var result = new Dictionary<object, object>(Items.Count);
                        foreach (var entry in Items)
                        {
                                var key = entry.Key?.GetValue();
                                var value = entry.Value?.GetValue();
                                result[key] = value;
                        }
                        return result;
                }
        }


	[MemoryPackable]
	public partial class TextureWrapper : TypedObject
	{
		public string TextureName { get; set; }

		private static readonly string[] PredefinedTextureFolders =
		{
			"", // Base Resources folder
			"textures", "Textures", "TEXTURES",
			"images", "Images", "IMAGES",
			"graphics", "TEXTURE", "GRAPHICS",
			"tex", "Tex", "Texture"
		};

		[MemoryPackConstructor]
		public TextureWrapper() { }

		public TextureWrapper(Texture texture)
		{
			if (texture == null)
			{
				Logger.Log("TextureWrapper: Provided Texture is null.", LogLevel.Info);
				return;
			}

			TextureName = texture.name;
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(TextureName))
			{
				Logger.Log("TextureWrapper: TextureName is null or empty.", LogLevel.Info);
				return null;
			}

			// Try loading the Texture from predefined folders
			foreach (var folder in PredefinedTextureFolders)
			{
				string path = string.IsNullOrEmpty(folder) ? TextureName : $"{folder}/{TextureName}";

                                var texture = AssetProvider.Load<Texture>(path);
				if (texture != null)
				{
					Logger.Log($"TextureWrapper: Found Texture '{TextureName}' at '{path}'.", LogLevel.Info);
					return texture;
				}
			}
#if REMEMBERME_GC2CORE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			// Fall back to using the registry
                        TextureRegistrySO registry = AssetProvider.Load<TextureRegistrySO>("TextureRegistry");
			if (registry == null)
			{
				Logger.Log("TextureWrapper: TextureRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			string resourcePath = registry.GetResourcePath(TextureName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"TextureWrapper: Texture '{TextureName}' is not registered in the registry.", LogLevel.Warning);
				return null;
			}

                        Texture textureFromRegistry = AssetProvider.Load<Texture>(resourcePath);
			if (textureFromRegistry != null)
			{
				Logger.Log($"TextureWrapper: Found Texture '{TextureName}' in registry at '{resourcePath}'.", LogLevel.Info);
				return textureFromRegistry;
			}

			Logger.Log($"TextureWrapper: Could not load Texture '{TextureName}' from registry path '{resourcePath}'.", LogLevel.Error);
#endif			
			return null;
		}
	}

	[MemoryPackable]
	public partial class MaterialWrapper : TypedObject
	{
		public string MaterialName { get; set; }
		public string ShaderName { get; set; }
		public Dictionary<string, string> TextureNames { get; set; } = new Dictionary<string, string>();

		private static readonly string[] PredefinedTextureFolders =
		{
			"", // Base Resources folder
			"mats", "Mats", "MATERIAL",
			"materials", "Materials", "MATERIALS",
			"mtls", "mtl", "Mtl",
			"Mat", "mat", "Mtls"
		};

		[MemoryPackConstructor]
		public MaterialWrapper() { }

		public MaterialWrapper(Material material)
		{
			if (material == null)
			{
				Logger.Log("MaterialWrapper: Provided material is null.", LogLevel.Info);
				return;
			}

			MaterialName = material.name;
			ShaderName = material.shader?.name;

			foreach (var textureProperty in material.GetTexturePropertyNames())
			{
				var texture = material.GetTexture(textureProperty);
				if (texture != null)
				{
					TextureNames[textureProperty] = texture.name;
				}
			}
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ShaderName))
			{
				Logger.Log($"MaterialWrapper: Shader name is missing for material '{MaterialName}'.", LogLevel.Info);
				return null;
			}

			Shader shader = Shader.Find(ShaderName);
			if (shader == null)
			{
				Logger.Log($"MaterialWrapper: Shader '{ShaderName}' not found.", LogLevel.Warning);
				return null;
			}

			var material = new Material(shader) { name = MaterialName };

			foreach (var kvp in TextureNames)
			{
				var texture = FindTexture(kvp.Value);
				if (texture != null)
				{
					material.SetTexture(kvp.Key, texture);
				}
				else
				{
					Logger.Log($"MaterialWrapper: Texture '{kvp.Value}' not found for property '{kvp.Key}'.", LogLevel.Warning);
				}
			}

			return material;
		}

		private Texture FindTexture(string textureName)
		{
			if (string.IsNullOrEmpty(textureName))
			{
				Logger.Log("MaterialWrapper: Texture name is null or empty.", LogLevel.Warning);
				return null;
			}

			// Try predefined folders
			foreach (var folder in PredefinedTextureFolders)
			{
				string path = string.IsNullOrEmpty(folder) ? textureName : $"{folder}/{textureName}";
                                var texture = AssetProvider.Load<Texture>(path);
				if (texture != null)
				{
					Logger.Log($"MaterialWrapper: Found texture '{textureName}' at path '{path}'.", LogLevel.Info);
					return texture;
				}
			}
#if REMEMBERME_GC2CORE_PRESENT && REMEMBERME_GC2MODULE_PRESENT && ARAWN_REMEMBERME && MEMORYPACK
			// Try registry
                        MaterialRegistrySO registry = AssetProvider.Load<MaterialRegistrySO>("MaterialRegistry");
			if (registry == null)
			{
				Logger.Log("MaterialWrapper: MaterialRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			string resourcePath = registry.GetResourcePath(textureName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"MaterialWrapper: Texture '{textureName}' is not registered in the registry.", LogLevel.Warning);
				return null;
			}

                        Texture textureFromRegistry = AssetProvider.Load<Texture>(resourcePath);
			if (textureFromRegistry != null)
			{
				Logger.Log($"MaterialWrapper: Found texture '{textureName}' in registry at '{resourcePath}'.", LogLevel.Info);
				return textureFromRegistry;
			}

			Logger.Log($"MaterialWrapper: Could not load texture '{textureName}' from registry path '{resourcePath}'.", LogLevel.Error);
#endif	
			return null;
		}
	}
#if REMEMBERME_GC2MELEE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2CORE_PRESENT && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
	[MemoryPackable]
	public partial class MeleeWeaponWrapper : TypedObject
	{
		public string WeaponName { get; set; }
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public MeleeWeaponWrapper() { }

		public MeleeWeaponWrapper(MeleeWeapon weapon)
		{
			if (weapon == null)
			{
				Logger.Log("MeleeWeaponWrapper: Provided MeleeWeapon is null.", LogLevel.Warning);
				return;
			}

			WeaponName = weapon.name;

			// Retrieve the resource path during runtime or editor mode
			ResourcePath = GetResourcePath(weapon);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"MeleeWeaponWrapper: Failed to determine resource path for weapon '{WeaponName}'.", LogLevel.Warning);
			}
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"MeleeWeaponWrapper: ResourcePath is null or empty for weapon '{WeaponName}'.", LogLevel.Warning);
				return null;
			}

                        var weapon = AssetProvider.Load<MeleeWeapon>(ResourcePath);
			if (weapon == null)
			{
				Logger.Log($"MeleeWeaponWrapper: Could not find MeleeWeapon at '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
			}
			else
			{
				Logger.Log($"MeleeWeaponWrapper: Successfully loaded MeleeWeapon '{WeaponName}' from '{ResourcePath}'.", LogLevel.Info);
			}
			return weapon;
		}

		private string GetResourcePath(MeleeWeapon weapon)
		{
#if UNITY_EDITOR
			// Editor-only: Extract path relative to the Resources folder
			string fullPath = AssetDatabase.GetAssetPath(weapon);
			int resourcesIndex = fullPath.IndexOf("/Resources/", StringComparison.Ordinal);
			if (resourcesIndex != -1)
			{
				string path = fullPath.Substring(resourcesIndex + "/Resources/".Length);
				return System.IO.Path.ChangeExtension(path, null); // Remove file extension
			}
			Logger.Log($"MeleeWeaponWrapper: Weapon '{weapon.name}' is not inside a Resources folder.", LogLevel.Warning);
			return null;
#else
				// Runtime: Cannot determine resource path dynamically
				Logger.Log("MeleeWeaponWrapper: Cannot extract resource path at runtime. Ensure assets are properly registered.", LogLevel.Error);
				return null;
#endif
		}
	}

	[MemoryPackable]
	public partial class SkillWrapper : TypedObject
	{
		/// <summary>
		/// The name of the Skill asset.
		/// </summary>
		public string SkillName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Melee/Skills/Fireball").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public SkillWrapper() { }

		public SkillWrapper(Skill skill)
		{
			if (skill == null)
			{
				Logger.Log("SkillWrapper: Provided Skill is null.", LogLevel.Warning);
				return;
			}

			SkillName = skill.name;

			// Retrieve the resource path during editor mode
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(skill);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"SkillWrapper: Failed to determine resource path for Skill '{SkillName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"SkillWrapper: ResourcePath is null or empty for Skill '{SkillName}'.", LogLevel.Warning);
				return null;
			}

			// Load the Skill asset from Resources using the resource path
                        var skill = AssetProvider.Load<Skill>(ResourcePath);
			if (skill != null)
			{
				Logger.Log($"SkillWrapper: Successfully loaded Skill '{SkillName}' from '{ResourcePath}'.", LogLevel.Info);
				return skill;
			}
			else
			{
				Logger.Log($"SkillWrapper: Failed to load Skill '{SkillName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Skill relative to any Resources folder.
		/// </summary>
		/// <param name="skill">The Skill asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(Skill skill)
		{
			string fullPath = AssetDatabase.GetAssetPath(skill);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"SkillWrapper: AssetDatabase could not find path for Skill '{skill.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"SkillWrapper: Skill '{skill.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif
	}

	[MemoryPackable]
	public partial class ShieldWrapper : TypedObject
	{
		/// <summary>
		/// The name of the Shield asset.
		/// </summary>
		public string ShieldName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Melee/Shields/IronShield").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public ShieldWrapper() { }

		public ShieldWrapper(Shield shield)
		{
			if (shield == null)
			{
				Logger.Log("ShieldWrapper: Provided Shield is null.", LogLevel.Warning);
				return;
			}

			ShieldName = shield.name;

			// Retrieve the resource path during editor mode
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(shield);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"ShieldWrapper: Failed to determine resource path for Shield '{ShieldName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"ShieldWrapper: ResourcePath is null or empty for Shield '{ShieldName}'.", LogLevel.Warning);
				return null;
			}

			// Load the Shield asset from Resources using the resource path
                        var shield = AssetProvider.Load<Shield>(ResourcePath);
			if (shield != null)
			{
				Logger.Log($"ShieldWrapper: Successfully loaded Shield '{ShieldName}' from '{ResourcePath}'.", LogLevel.Info);
				return shield;
			}
			else
			{
				Logger.Log($"ShieldWrapper: Failed to load Shield '{ShieldName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Shield relative to any Resources folder.
		/// </summary>
		/// <param name="shield">The Shield asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(Shield shield)
		{
			string fullPath = AssetDatabase.GetAssetPath(shield);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"ShieldWrapper: AssetDatabase could not find path for Shield '{shield.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"ShieldWrapper: Shield '{shield.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif
	}
#endif

#if REMEMBERME_GC2SHOOTER_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2CORE_PRESENT && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
	[MemoryPackable]
	public partial class ShooterWeaponWrapper : TypedObject
	{
		/// <summary>
		/// The name of the ShooterWeapon asset.
		/// </summary>
		public string ShooterWeaponName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Shooter/ShooterWeapons/AssaultRifle").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public ShooterWeaponWrapper() { }

		public ShooterWeaponWrapper(ShooterWeapon shooterWeapon)
		{
			if (shooterWeapon == null)
			{
				Logger.Log("ShooterWeaponWrapper: Provided ShooterWeapon is null.", LogLevel.Warning);
				return;
			}

			ShooterWeaponName = shooterWeapon.name;

#if UNITY_EDITOR
			// Retrieve the resource path during editor mode
			ResourcePath = GetResourcePath(shooterWeapon);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"ShooterWeaponWrapper: Failed to determine resource path for ShooterWeapon '{ShooterWeaponName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"ShooterWeaponWrapper: ResourcePath is null or empty for ShooterWeapon '{ShooterWeaponName}'.", LogLevel.Warning);
				return null;
			}

			// Load the ShooterWeapon asset from Resources using the resource path
                        var shooterWeapon = AssetProvider.Load<ShooterWeapon>(ResourcePath);
			if (shooterWeapon != null)
			{
				Logger.Log($"ShooterWeaponWrapper: Successfully loaded ShooterWeapon '{ShooterWeaponName}' from '{ResourcePath}'.", LogLevel.Info);
				return shooterWeapon;
			}
			else
			{
				Logger.Log($"ShooterWeaponWrapper: Failed to load ShooterWeapon '{ShooterWeaponName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided ShooterWeapon relative to any Resources folder.
		/// </summary>
		/// <param name="shooterWeapon">The ShooterWeapon asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(ShooterWeapon shooterWeapon)
		{
			string fullPath = AssetDatabase.GetAssetPath(shooterWeapon);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"ShooterWeaponWrapper: AssetDatabase could not find path for ShooterWeapon '{shooterWeapon.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"ShooterWeaponWrapper: ShooterWeapon '{shooterWeapon.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif
	}
#endif

#if REMEMBERME_GC2QUESTS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2CORE_PRESENT && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
	[MemoryPackable]
	public partial class QuestWrapper : TypedObject
	{
		/// <summary>
		/// The unique identifier of the Quest asset.
		/// </summary>
		public string QuestName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Quests/Quest01").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public QuestWrapper() { }

		public QuestWrapper(Quest quest)
		{
			if (quest == null)
			{
				Logger.Log("QuestWrapper: Provided Quest is null.", LogLevel.Warning);
				return;
			}

			QuestName = quest.name;
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(quest);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"QuestWrapper: Failed to determine resource path for Quest '{QuestName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"QuestWrapper: ResourcePath is null or empty for Quest '{QuestName}'. Attempting to retrieve from Quest Registry.", LogLevel.Info);
				ResourcePath = GetResourcePathFromRegistry(QuestName);
				if (string.IsNullOrEmpty(ResourcePath))
				{
					Logger.Log($"QuestWrapper: Unable to retrieve resource path for Quest '{QuestName}' from Quest Registry.", LogLevel.Error);
					return null;
				}
			}

			// Attempt to load the Quest asset from Resources using the resource path
                        var quest = AssetProvider.Load<Quest>(ResourcePath);
			if (quest != null)
			{
				Logger.Log($"QuestWrapper: Successfully loaded Quest '{QuestName}' from '{ResourcePath}'.", LogLevel.Info);
				return quest;
			}
			else
			{
				Logger.Log($"QuestWrapper: Failed to load Quest '{QuestName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Quest relative to any Resources folder.
		/// </summary>
		/// <param name="quest">The Quest asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(Quest quest)
		{
			string fullPath = AssetDatabase.GetAssetPath(quest);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"QuestWrapper: AssetDatabase could not find path for Quest '{quest.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"QuestWrapper: Quest '{quest.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif

		/// <summary>
		/// Attempts to retrieve the resource path for a Quest from the Quest Registry based on its name.
		/// </summary>
		/// <param name="questName">The name of the Quest.</param>
		/// <returns>The resource path if found; otherwise, null.</returns>
		private string GetResourcePathFromRegistry(string questName)
		{
			// Load the Quest Registry
                        QuestRegistrySO questRegistry = AssetProvider.Load<QuestRegistrySO>("QuestRegistry");
			if (questRegistry == null)
			{
				Logger.Log("QuestWrapper: QuestRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			// Retrieve the resource path from the registry
			string resourcePath = questRegistry.GetResourcePath(questName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"QuestWrapper: Quest '{questName}' is not registered in the Quest Registry.", LogLevel.Warning);
				return null;
			}

			return resourcePath;
		}
	}
#endif

#if REMEMBERME_GC2INVENTORY_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2CORE_PRESENT && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
	[MemoryPackable]
	public partial class ItemWrapper : TypedObject
	{
		/// <summary>
		/// The unique identifier of the Item asset.
		/// </summary>
		public string ItemName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Inventory/Items/Sword").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public ItemWrapper() { }

		public ItemWrapper(Item item)
		{
			if (item == null)
			{
				Logger.Log("ItemWrapper: Provided Item is null.", LogLevel.Warning);
				return;
			}

			ItemName = item.name;
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(item);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"ItemWrapper: Failed to determine resource path for Item '{ItemName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"ItemWrapper: ResourcePath is null or empty for Item '{ItemName}'. Attempting to retrieve from Item Registry.", LogLevel.Info);
				ResourcePath = GetResourcePathFromRegistry(ItemName);
				if (string.IsNullOrEmpty(ResourcePath))
				{
					Logger.Log($"ItemWrapper: Unable to retrieve resource path for Item '{ItemName}' from Item Registry.", LogLevel.Error);
					return null;
				}
			}

			// Attempt to load the Item asset from Resources using the resource path
                        var item = AssetProvider.Load<Item>(ResourcePath);
			if (item != null)
			{
				Logger.Log($"ItemWrapper: Successfully loaded Item '{ItemName}' from '{ResourcePath}'.", LogLevel.Info);
				return item;
			}
			else
			{
				Logger.Log($"ItemWrapper: Failed to load Item '{ItemName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Item relative to any Resources folder.
		/// </summary>
		/// <param name="item">The Item asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(Item item)
		{
			string fullPath = AssetDatabase.GetAssetPath(item);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"ItemWrapper: AssetDatabase could not find path for Item '{item.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"ItemWrapper: Item '{item.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif

		/// <summary>
		/// Attempts to retrieve the resource path for an Item from the Item Registry based on its name.
		/// </summary>
		/// <param name="itemName">The name of the Item.</param>
		/// <returns>The resource path if found; otherwise, null.</returns>
		private string GetResourcePathFromRegistry(string itemName)
		{
			// Load the Item Registry
                        ItemRegistrySO itemRegistry = AssetProvider.Load<ItemRegistrySO>("ItemRegistry");
			if (itemRegistry == null)
			{
				Logger.Log("ItemWrapper: ItemRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			// Retrieve the resource path from the registry
			string resourcePath = itemRegistry.GetResourcePath(itemName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"ItemWrapper: Item '{itemName}' is not registered in the Item Registry.", LogLevel.Warning);
				return null;
			}

			return resourcePath;
		}
	}
#endif

#if REMEMBERME_GC2STATS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2CORE_PRESENT && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
	[MemoryPackable]
	public partial class AttributeWrapper : TypedObject
	{
		/// <summary>
		/// The name of the Attribute asset.
		/// </summary>
		public string AttributeName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Stats/Attributes/Strength").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public AttributeWrapper() { }

		public AttributeWrapper(GameCreator.Runtime.Stats.Attribute attribute)
		{
			if (attribute == null)
			{
				Logger.Log("AttributeWrapper: Provided Attribute is null.", LogLevel.Warning);
				return;
			}

			AttributeName = attribute.name;
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(attribute);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"AttributeWrapper: Failed to determine resource path for Attribute '{AttributeName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"AttributeWrapper: ResourcePath is null or empty for Attribute '{AttributeName}'. Attempting to retrieve from Attribute Registry.", LogLevel.Info);
				ResourcePath = GetResourcePathFromRegistry(AttributeName);
				if (string.IsNullOrEmpty(ResourcePath))
				{
					Logger.Log($"AttributeWrapper: Unable to retrieve resource path for Attribute '{AttributeName}' from Attribute Registry.", LogLevel.Error);
					return null;
				}
			}

			// Attempt to load the Attribute asset from Resources using the resource path
                        var attribute = AssetProvider.Load<GameCreator.Runtime.Stats.Attribute>(ResourcePath);
			if (attribute != null)
			{
				Logger.Log($"AttributeWrapper: Successfully loaded Attribute '{AttributeName}' from '{ResourcePath}'.", LogLevel.Info);
				return attribute;
			}
			else
			{
				Logger.Log($"AttributeWrapper: Failed to load Attribute '{AttributeName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Attribute relative to any Resources folder.
		/// </summary>
		/// <param name="attribute">The Attribute asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(GameCreator.Runtime.Stats.Attribute attribute)
		{
			string fullPath = UnityEditor.AssetDatabase.GetAssetPath(attribute);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"AttributeWrapper: AssetDatabase could not find path for Attribute '{attribute.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"AttributeWrapper: Attribute '{attribute.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif

		/// <summary>
		/// Attempts to retrieve the resource path for an Attribute from the Attribute Registry based on its name.
		/// </summary>
		/// <param name="attributeName">The name of the Attribute.</param>
		/// <returns>The resource path if found; otherwise, null.</returns>
		private string GetResourcePathFromRegistry(string attributeName)
		{
			// Load the Attribute Registry
                        AttributeRegistrySO attributeRegistry = AssetProvider.Load<AttributeRegistrySO>("AttributeRegistry");
			if (attributeRegistry == null)
			{
				Logger.Log("AttributeWrapper: AttributeRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			// Retrieve the resource path from the registry
			string resourcePath = attributeRegistry.GetResourcePath(attributeName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"AttributeWrapper: Attribute '{attributeName}' is not registered in the Attribute Registry.", LogLevel.Warning);
				return null;
			}

			return resourcePath;
		}
	}

	[MemoryPackable]
	public partial class StatWrapper : TypedObject
	{
		/// <summary>
		/// The name of the Stat asset.
		/// </summary>
		public string StatName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Stats/Stats/Strength").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public StatWrapper() { }

		public StatWrapper(Stat stat)
		{
			if (stat == null)
			{
				Logger.Log("StatWrapper: Provided Stat is null.", LogLevel.Warning);
				return;
			}

			StatName = stat.name;
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(stat);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"StatWrapper: Failed to determine resource path for Stat '{StatName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"StatWrapper: ResourcePath is null or empty for Stat '{StatName}'. Attempting to retrieve from Stat Registry.", LogLevel.Info);
				ResourcePath = GetResourcePathFromRegistry(StatName);
				if (string.IsNullOrEmpty(ResourcePath))
				{
					Logger.Log($"StatWrapper: Unable to retrieve resource path for Stat '{StatName}' from Stat Registry.", LogLevel.Error);
					return null;
				}
			}

			// Attempt to load the Stat asset from Resources using the resource path
                        var stat = AssetProvider.Load<Stat>(ResourcePath);
			if (stat != null)
			{
				Logger.Log($"StatWrapper: Successfully loaded Stat '{StatName}' from '{ResourcePath}'.", LogLevel.Info);
				return stat;
			}
			else
			{
				Logger.Log($"StatWrapper: Failed to load Stat '{StatName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Stat relative to any Resources folder.
		/// </summary>
		/// <param name="stat">The Stat asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(Stat stat)
		{
			string fullPath = UnityEditor.AssetDatabase.GetAssetPath(stat);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"StatWrapper: AssetDatabase could not find path for Stat '{stat.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"StatWrapper: Stat '{stat.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif

		/// <summary>
		/// Attempts to retrieve the resource path for a Stat from the Stat Registry based on its name.
		/// </summary>
		/// <param name="statName">The name of the Stat.</param>
		/// <returns>The resource path if found; otherwise, null.</returns>
		private string GetResourcePathFromRegistry(string statName)
		{
			// Load the Stat Registry
                        StatRegistrySO statRegistry = AssetProvider.Load<StatRegistrySO>("StatRegistry");
			if (statRegistry == null)
			{
				Logger.Log("StatWrapper: StatRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			// Retrieve the resource path from the registry
			string resourcePath = statRegistry.GetResourcePath(statName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"StatWrapper: Stat '{statName}' is not registered in the Stat Registry.", LogLevel.Warning);
				return null;
			}

			return resourcePath;
		}
	}

	[MemoryPackable]
	public partial class StatusEffectWrapper : TypedObject
	{
		/// <summary>
		/// The name of the StatusEffect asset.
		/// </summary>
		public string StatusEffectName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Stats/StatusEffects/Burning").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public StatusEffectWrapper() { }

		public StatusEffectWrapper(StatusEffect statusEffect)
		{
			if (statusEffect == null)
			{
				Logger.Log("StatusEffectWrapper: Provided StatusEffect is null.", LogLevel.Warning);
				return;
			}

			StatusEffectName = statusEffect.name;

#if UNITY_EDITOR
			ResourcePath = GetResourcePath(statusEffect);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"StatusEffectWrapper: Failed to determine resource path for StatusEffect '{StatusEffectName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"StatusEffectWrapper: ResourcePath is null or empty for StatusEffect '{StatusEffectName}'. Attempting to retrieve from StatusEffect Registry.", LogLevel.Info);
				ResourcePath = GetResourcePathFromRegistry(StatusEffectName);
				if (string.IsNullOrEmpty(ResourcePath))
				{
					Logger.Log($"StatusEffectWrapper: Unable to retrieve resource path for StatusEffect '{StatusEffectName}' from StatusEffect Registry.", LogLevel.Error);
					return null;
				}
			}

			// Attempt to load the StatusEffect asset from Resources using the resource path
                        var statusEffect = AssetProvider.Load<StatusEffect>(ResourcePath);
			if (statusEffect != null)
			{
				Logger.Log($"StatusEffectWrapper: Successfully loaded StatusEffect '{StatusEffectName}' from '{ResourcePath}'.", LogLevel.Info);
				return statusEffect;
			}
			else
			{
				Logger.Log($"StatusEffectWrapper: Failed to load StatusEffect '{StatusEffectName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided StatusEffect relative to any Resources folder.
		/// </summary>
		/// <param name="statusEffect">The StatusEffect asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(StatusEffect statusEffect)
		{
			string fullPath = UnityEditor.AssetDatabase.GetAssetPath(statusEffect);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"StatusEffectWrapper: AssetDatabase could not find path for StatusEffect '{statusEffect.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", System.StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"StatusEffectWrapper: StatusEffect '{statusEffect.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif

		/// <summary>
		/// Attempts to retrieve the resource path for a StatusEffect from the StatusEffect Registry based on its name.
		/// </summary>
		/// <param name="statusEffectName">The name of the StatusEffect.</param>
		/// <returns>The resource path if found; otherwise, null.</returns>
		private string GetResourcePathFromRegistry(string statusEffectName)
		{
			// Load the StatusEffect Registry
                        StatusEffectRegistrySO statusEffectRegistry = AssetProvider.Load<StatusEffectRegistrySO>("StatusEffectRegistry");
			if (statusEffectRegistry == null)
			{
				Logger.Log("StatusEffectWrapper: StatusEffectRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			// Retrieve the resource path from the registry
			string resourcePath = statusEffectRegistry.GetResourcePath(statusEffectName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"StatusEffectWrapper: StatusEffect '{statusEffectName}' is not registered in the StatusEffect Registry.", LogLevel.Warning);
				return null;
			}

			return resourcePath;
		}
	}

	[MemoryPackable]
        public partial class FormulaWrapper : TypedObject
        {
		/// <summary>
		/// The name of the Formula asset.
		/// </summary>
		public string FormulaName { get; set; }

		/// <summary>
		/// The resource path relative to any Resources folder (e.g., "GameCreator/Stats/Formulas/DamageCalculation").
		/// </summary>
		public string ResourcePath { get; set; }

		[MemoryPackConstructor]
		public FormulaWrapper() { }

		public FormulaWrapper(Formula formula)
		{
			if (formula == null)
			{
				Logger.Log("FormulaWrapper: Provided Formula is null.", LogLevel.Warning);
				return;
			}

			FormulaName = formula.name;
#if UNITY_EDITOR
			ResourcePath = GetResourcePath(formula);
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"FormulaWrapper: Failed to determine resource path for Formula '{FormulaName}'. Ensure it's inside a Resources folder.", LogLevel.Warning);
			}
#endif
		}

		public override object GetValue()
		{
			if (string.IsNullOrEmpty(ResourcePath))
			{
				Logger.Log($"FormulaWrapper: ResourcePath is null or empty for Formula '{FormulaName}'. Attempting to retrieve from Formula Registry.", LogLevel.Info);
				ResourcePath = GetResourcePathFromRegistry(FormulaName);
				if (string.IsNullOrEmpty(ResourcePath))
				{
					Logger.Log($"FormulaWrapper: Unable to retrieve resource path for Formula '{FormulaName}' from Formula Registry.", LogLevel.Error);
					return null;
				}
			}

			// Attempt to load the Formula asset from Resources using the resource path
                        var formula = AssetProvider.Load<Formula>(ResourcePath);
			if (formula != null)
			{
				Logger.Log($"FormulaWrapper: Successfully loaded Formula '{FormulaName}' from '{ResourcePath}'.", LogLevel.Info);
				return formula;
			}
			else
			{
				Logger.Log($"FormulaWrapper: Failed to load Formula '{FormulaName}' from '{ResourcePath}'. Ensure the asset exists in the Resources folder.", LogLevel.Error);
				return null;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Retrieves the resource path of the provided Formula relative to any Resources folder.
		/// </summary>
		/// <param name="formula">The Formula asset.</param>
		/// <returns>The relative resource path as a string, or null if not found within Resources.</returns>
		private string GetResourcePath(Formula formula)
		{
			string fullPath = UnityEditor.AssetDatabase.GetAssetPath(formula);
			if (string.IsNullOrEmpty(fullPath))
			{
				Logger.Log($"FormulaWrapper: AssetDatabase could not find path for Formula '{formula.name}'.", LogLevel.Warning);
				return null;
			}

			// Find the last occurrence of "Resources/" in the path
			int resourcesIndex = fullPath.LastIndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
			if (resourcesIndex < 0)
			{
				Logger.Log($"FormulaWrapper: Formula '{formula.name}' is not inside a Resources folder.", LogLevel.Warning);
				return null;
			}

			// Extract the path relative to the Resources folder and remove the file extension
			string relativePath = fullPath.Substring(resourcesIndex + "Resources/".Length);
			relativePath = System.IO.Path.ChangeExtension(relativePath, null); // Remove extension
			relativePath = relativePath.Replace('\\', '/'); // Ensure consistency with forward slashes

			return relativePath;
		}
#endif

		/// <summary>
		/// Attempts to retrieve the resource path for a Formula from the Formula Registry based on its name.
		/// </summary>
		/// <param name="formulaName">The name of the Formula.</param>
		/// <returns>The resource path if found; otherwise, null.</returns>
                private string GetResourcePathFromRegistry(string formulaName)
                {
			// Load the Formula Registry
                        FormulaRegistrySO formulaRegistry = AssetProvider.Load<FormulaRegistrySO>("FormulaRegistry");
			if (formulaRegistry == null)
			{
				Logger.Log("FormulaWrapper: FormulaRegistrySO not found in Resources.", LogLevel.Warning);
				return null;
			}

			// Retrieve the resource path from the registry
			string resourcePath = formulaRegistry.GetResourcePath(formulaName);
			if (string.IsNullOrEmpty(resourcePath))
			{
				Logger.Log($"FormulaWrapper: Formula '{formulaName}' is not registered in the Formula Registry.", LogLevel.Warning);
				return null;
			}

                        return resourcePath;
                }
        }

#endif
}
#endif