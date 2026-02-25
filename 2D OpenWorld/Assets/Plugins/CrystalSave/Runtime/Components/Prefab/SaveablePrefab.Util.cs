// SaveablePrefab.Util.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Arawn.CrystalSave.Runtime
{
	internal static class SaveablePrefabUtil
	{
	 /// <summary>
    /// Builds a slash-separated path (<c>"Arm/Hand/Index"</c>) *relative* to a
    /// given <paramref name="root"/>.  If <paramref name="root"/> is omitted
    /// the topmost Transform is used, which is fine when you later strip the
    /// prefab’s own name anyway.
    /// </summary>
    internal static string GetHierarchyPath(UnityEngine.Transform tr,
                                            UnityEngine.Transform root = null)
    {
        if (!tr) return string.Empty;

        root ??= tr.root;

        var names = new System.Collections.Generic.List<string>(8);
        for (var cur = tr; cur != null && cur != root; cur = cur.parent)
            names.Add(cur.name);

        names.Reverse();                         // bottom-up → top-down
        return string.Join("/", names);
    }

		/* ───── Cached reflection helpers ─────────────────────────── */
		private static readonly Dictionary<Type, bool> _typeSupport =
			new Dictionary<Type, bool>();

		private static readonly Dictionary<string, Type> _nameLookup =
			new Dictionary<string, Type>();

		/// <summary>
		/// Clears the type name lookup cache. Call this if types are being resolved incorrectly.
		/// </summary>
		internal static void ClearTypeCache()
		{
			_nameLookup.Clear();
		}

		internal static bool IsSupported(Type t)
		{
			if (_typeSupport.TryGetValue(t, out bool ok)) return ok;
			ok = t.IsPrimitive || t == typeof(string) || t == typeof(UnityEngine.Vector3) ||
				 t == typeof(UnityEngine.Quaternion) || t == typeof(float) ||
				 t == typeof(double) || t == typeof(bool) || t == typeof(int);
			_typeSupport[t] = ok;
			return ok;
		}

		internal static Type FindByName(string name)
		{
			if (_nameLookup.TryGetValue(name, out var t)) return t;

			// fast path - try fully qualified name
			t = Type.GetType(name);
			if (t != null) { _nameLookup[name] = t; return t; }

			// Priority 1: Check UnityEngine assemblies first for common Unity types
			// This prevents finding stub/mock types from other assemblies
			var unityAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.GetName().Name.StartsWith("UnityEngine"))
				.ToList();

			foreach (var asm in unityAssemblies)
			{
				try
				{
					t = asm.GetTypes().FirstOrDefault(x => x.Name == name && typeof(UnityEngine.Component).IsAssignableFrom(x));
					if (t != null) { _nameLookup[name] = t; return t; }
				}
				catch (ReflectionTypeLoadException)
				{
					// Some assemblies may have loading issues, skip them
				}
			}

			// Priority 2: Check all other assemblies for custom component types
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (asm.GetName().Name.StartsWith("UnityEngine")) continue; // already checked
				try
				{
					t = asm.GetTypes().FirstOrDefault(x => x.Name == name && typeof(UnityEngine.Component).IsAssignableFrom(x));
					if (t != null) { _nameLookup[name] = t; return t; }
				}
				catch (ReflectionTypeLoadException)
				{
					// Some assemblies may have loading issues, skip them
				}
			}

			return null;
		}

		/* ───── Generic hierarchy helpers ─────────────────────────── */
		internal static IEnumerable<string> GetAllDescendants(UnityEngine.Transform root)
		{
			if (!root) yield break;
			var stack = new Stack<(UnityEngine.Transform, string)>();
			stack.Push((root, ""));
			while (stack.Count > 0)
			{
				var (t, path) = stack.Pop();
				for (int i = 0; i < t.childCount; ++i)
				{
					var c = t.GetChild(i);
					string p = string.IsNullOrEmpty(path) ? c.name : $"{path}/{c.name}";
					yield return p;
					stack.Push((c, p));
				}
			}
		}
	}
}
