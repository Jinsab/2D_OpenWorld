using System;
#if UNITY_EDITOR
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#endif
#if REMEMBERME_GC2CORE_PRESENT
using GameCreator.Runtime.Common;
#endif

namespace Arawn.CrystalSave
{
	// ─────────────────────────────────────────────────────────────────────────────
	//  Built-in name  → [RememberIcon("Camera Icon")]
	//  Built-in type  → [RememberTarget(typeof(Camera))]
	//  Asset path     → [RememberCustomIcon("Assets/…/MyIcon.png")]
	//  Inline bytes   → [RememberInlineIcon(typeof(MyIconBytes))]
	//  GC-Core icon   → [RememberGC2Icon(typeof(IconAbsolute))]      ← NEW
	// ─────────────────────────────────────────────────────────────────────────────

	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
#if UNITY_EDITOR
	[Conditional("UNITY_EDITOR")]
#endif
	public sealed class RememberIconAttribute : Attribute
	{
		public string IconName { get; }
		public RememberIconAttribute(string iconName) => IconName = iconName;
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
#if UNITY_EDITOR
	[Conditional("UNITY_EDITOR")]
#endif
	public sealed class RememberTargetAttribute : Attribute
	{
		public Type TargetType { get; }
		public RememberTargetAttribute(Type targetType) => TargetType = targetType;
	}

	/// <summary>Points to a texture asset on disk (path or GUID).</summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
#if UNITY_EDITOR
	[Conditional("UNITY_EDITOR")]
#endif
	public sealed class RememberCustomIconAttribute : Attribute
	{
		public string AssetPathOrGuid { get; }
		public RememberCustomIconAttribute(string assetPathOrGuid)
			=> AssetPathOrGuid = assetPathOrGuid;
	}

	/// <summary>
	/// Points to a generated class exposing <c>Width</c>, <c>Height</c> and raw
	/// RGBA32 data.  Editor-only → no texture in the player build.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
#if UNITY_EDITOR
	[Conditional("UNITY_EDITOR")]
#endif
	public sealed class RememberInlineIconAttribute : Attribute
	{
		public Type ByteSourceType { get; }
		public RememberInlineIconAttribute(Type byteSourceType)
			=> ByteSourceType = byteSourceType;
	}

	// ─────────────────────────────────────────────────────────────────────────────
	//  Game-Creator 2 Core icon support (compile-time gated)
	// ─────────────────────────────────────────────────────────────────────────────
#if REMEMBERME_GC2CORE_PRESENT

    /// <summary>
    /// Declares a Game Creator 2 Core icon generator (<c>IIcon</c> implementation).
    /// <example>
    /// [RememberGC2Icon(typeof(IconAbsolute))] // blue default tint  
    /// [RememberGC2Icon(typeof(IconAbsolute), ColorTheme.Type.Red)]
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
#if UNITY_EDITOR
    [Conditional("UNITY_EDITOR")]
#endif
    public sealed class RememberGC2IconAttribute : ImageAttribute
    {
        public Type IconProviderType { get; }

        // default blue tint
        public RememberGC2IconAttribute(Type iconProvider)
            : this(iconProvider, ColorTheme.Type.Blue) { }

        public RememberGC2IconAttribute(Type iconProvider, ColorTheme.Type tint)
            : base(iconProvider, tint)
        {
            IconProviderType = iconProvider;
        }
    }

    // -------------------------------------------------------------------------
    //  Static helper that assigns the textures to MonoScript assets (Editor-only)
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    [InitializeOnLoad]
    internal static class GC2IconApplier
    {
        static GC2IconApplier() => ApplyIcons();

        private static void ApplyIcons()
        {
            // locate every class that has [RememberGC2Icon]
            foreach (Type t in TypeCache.GetTypesWithAttribute<RememberGC2IconAttribute>())
            {
                var attr = t.GetCustomAttribute<RememberGC2IconAttribute>();
                if (attr == null) continue;

                // instantiate provider → texture
                if (!(Activator.CreateInstance(attr.IconProviderType,
                       ColorTheme.Get(ColorTheme.Type.White), null) is IIcon provider))
                    continue;

                Texture2D tex = provider.Texture;
                if (tex == null) continue;
                tex.hideFlags = HideFlags.HideAndDontSave;

                // locate the MonoScript asset for this type
                MonoScript script = Resources.FindObjectsOfTypeAll<MonoScript>()
                                             .FirstOrDefault(ms => ms.GetClass() == t);
                if (script != null)
                {
                    EditorGUIUtility.SetIconForObject(script, tex);
                }
            }
        }
    }
#endif  // UNITY_EDITOR
#endif  // REMEMBERME_GC2CORE_PRESENT
}
