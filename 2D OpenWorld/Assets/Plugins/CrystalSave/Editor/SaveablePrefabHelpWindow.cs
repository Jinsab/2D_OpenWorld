// SaveablePrefabHelpWindow.cs  ©2025 Arawn – Crystal Save
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Arawn.CrystalSave.Editor
{
	public class SaveablePrefabHelpWindow : EditorWindow
	{
		private const int WIDTH = 460;
		private const int HEIGHT = 620;

		/*─────────────────── menu & aliases ───────────────────*/
		[MenuItem("Tools/Crystal Save/Help/Remember Prefab Help")]
		public static void Open()
		{
			var w = GetWindow<SaveablePrefabHelpWindow>();
			w.titleContent = new GUIContent("Remember Prefab – Help");
			w.minSize = new Vector2(WIDTH, HEIGHT);
			w.ShowUtility();
		}

		// ➜ Compatibility for the inspector button expecting ShowWindow()
		public static void ShowWindow() => Open();

		/*─────────────────── UI Toolkit build ───────────────────*/
                private void CreateGUI()
                {
                        var root = rootVisualElement;

                        var scroll = new ScrollView(ScrollViewMode.Vertical)
                        {
                                style =
                        {
                                flexGrow        = 1,
                                paddingLeft     = 6,
                                paddingRight    = 6,
                                backgroundColor = EditorGUIUtility.isProSkin
                                        ? new Color(0.18f, 0.18f, 0.18f, 1f)
                                        : new Color(0.82f, 0.82f, 0.82f, 1f)
                        }
                        };
                        root.Add(scroll);

			/* helper lambdas */
			Label H(string text)
			{
				var l = new Label(text)
				{
					style =
				{
					unityFontStyleAndWeight = FontStyle.Bold,
					unityTextAlign          = TextAnchor.MiddleLeft,
					marginTop               = 4,
					marginBottom            = 2
				}
				};
				return l;
			}

			Label P(string text) => new Label(text) { style = { whiteSpace = WhiteSpace.Normal } };

                        Label Code(string snippet)
                        {
                                var l = new Label(snippet)
                                {
                                        style =
                                {
                                        unityFont       = EditorStyles.miniFont,
                                        whiteSpace      = WhiteSpace.NoWrap,
                                        backgroundColor = EditorGUIUtility.isProSkin
                                                ? new Color(0.13f, 0.13f, 0.13f, 1f)
                                                : new Color(0.90f, 0.90f, 0.90f, 1f),
                                        paddingLeft     = 4,
                                        paddingRight    = 4,
                                        marginTop       = 1,
                                        marginBottom    = 1
                                }
                                };
                                return l;
                        }

			HelpBox Info(string msg, HelpBoxMessageType t = HelpBoxMessageType.Info) => new HelpBox(msg, t);

			/*──────────────────── content (unchanged) ────────────────────*/
			scroll.Add(H("Remember Prefab aka SaveablePrefab – What Gets Serialized"));
			scroll.Add(Info("A Remember Prefab captures the *entire runtime state* of a prefab "
						  + "instance so it can be destroyed (or pooled) and recreated exactly as it was."));

			scroll.Add(H("✦ Always Serialized"));
                        scroll.Add(P(
                                "• Transform  – position, rotation, local scale\n" +
                                "• Parent link – remembers whether it was scene-root or under another Saveable/scene object\n" +
                                "• Prefab GameObject – tracks if this object was disabled or destroyed\n" +
                                "• Children GameObjects – remembers whether a child was deactivated or enabled or destroyed\n" +
                                "• Rigidbody  – linear & angular velocity, drag, gravity, kinematic flag\n" +
                                "• Animator   – current state hash + normalised time\n" +
                                "• Visibility – activeSelf + custom visibility flags\n" +
                                "• Runtime-added Components – full binary snapshot\n" +
                                "• Particle Systems – time & playing state\n" +
                                "• Mesh/Material overrides (at runtime)\n" +
                                "• Colliders – enabled/trigger plus shape data"));

			scroll.Add(H("✦ Inspector Fields (When & Why)"));
			scroll.Add(P(
                                "<b>Keep Across Scenes</b>\n" +
                                "• Survive scene loads (DontDestroyOnLoad).\n\n" +
                                "<b>Remember Home Scene</b>\n" +
                                "• Restore only when its original scene is loaded.\n" +
                                "• Turns off Keep Across Scenes and Off-screen settings.\n\n" +
                                "<b>Visible In Scenes</b>\n" +
				"• List of scenes where it stays active. Others trigger Off-screen mask.\n\n" +
				"<b>Off-screen Deactivation</b>\n" +
				"• What to disable outside those scenes (renderers, colliders, etc.).\n\n" +
				"<b>Register With Save System</b>\n" +
				"<i>Leave true unless you spawn short-lived cosmetic objects.</i>\n" +
				"Factories and pools set this automatically (See Instantiation & Pooling).\n\n" +
				"<b>Unique ID / Prefab Asset ID</b> (read-only)\n" +
				"• Runtime instance ID and stable asset ID – assigned automatically; don’t edit."));

			scroll.Add(H("✦ Child Identification"));
			scroll.Add(Info(
				"Path-based by default; add “Remember Component” (RememberComposite) + modules for renaming "
			  + "or re-parenting children.", HelpBoxMessageType.Warning));

			scroll.Add(H("✦ Instantiation & Pooling"));
			scroll.Add(P("<b>Plain C#</b>"));
			scroll.Add(Code("var sp = SaveablePrefabFactory.Instantiate(prefab, pos, rot);"));
			scroll.Add(Code("var pooled = SaveablePrefabPoolCache.Get(prefab, 20, true);"));
			scroll.Add(Code("var inst   = pooled.Spawn(pos, rot);"));
			scroll.Add(P("\n<b>Game Creator 2</b>"));
			scroll.Add(P("• Instantiate Saveable Prefab\n• Spawn From Pool\n• Despawn To Pool"));

			scroll.Add(H("✦ Supported Edge-Cases"));
			scroll.Add(P("• Pools recreated after load  • Trigger colliders restored  • Destroyed objects stay gone"));

			scroll.Add(H("✦ Limitations & Work-arounds"));
			scroll.Add(P(
				"• Path-based children break if renamed/re-parented → add Remember Component + modules.\n" +
                                "• New components after a save require saving again.\n" +
                                "• Adding RememberGameObject to a SaveablePrefab instance is redundant – it already tracks GameObject state.\n" +
                                "• Deep nested objects outside this prefab need another Remember Prefab or Remember Component."));

			/* close button */
			var close = new Button(Close) { text = "Close" };
			close.style.marginTop = 6;
			scroll.Add(close);
		}
	}
}

#endif
