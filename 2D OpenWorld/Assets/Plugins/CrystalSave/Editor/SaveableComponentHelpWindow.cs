// SaveableComponentHelpWindow.cs  ©2025 Arawn – Crystal Save
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Arawn.CrystalSave.Editor
{
	public class SaveableComponentHelpWindow : EditorWindow
	{
		private const int WIDTH = 440;
		private const int HEIGHT = 620;

		/*── Menu entry & alias for inspector button ─────────────────────────*/
		[MenuItem("Tools/Crystal Save/Help/Remember Component Help")]
		public static void Open()
		{
			var w = GetWindow<SaveableComponentHelpWindow>();
			w.titleContent = new GUIContent("Remember Component – Help");
			w.minSize = new Vector2(WIDTH, HEIGHT);
			w.ShowUtility();
		}
		public static void ShowWindow() => Open();

		/*── UI Toolkit build – keep identical styling to Prefab help ───────*/
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

			/* local helpers */
			Label H(string txt) => new(txt) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4, marginBottom = 2 } };
			Label P(string txt) => new(txt) { style = { whiteSpace = WhiteSpace.Normal } };
			HelpBox Info(string msg, HelpBoxMessageType t = HelpBoxMessageType.Info) => new(msg, t);

			/*──────────────── window content ───────────────────────────────*/
			scroll.Add(H("Remember Component aka SaveableComponent – Quick Reference"));
			scroll.Add(Info("A Remember XX Component serialises **one specific aspect** "
						  + "of the GameObject it sits on. Add several to cover "
						  + "everything you need or just wrap them with a Remember Component (RememberComposite)"
						  + "(shown as 'Remember Component' in the Inspector."));

			scroll.Add(H("✦ What a SaveableComponent Does"));
			scroll.Add(P("• Holds its own <b>ComponentID</b> for cross-scene look‑ups.\n"
					   + "• Captures and restores exactly the data its subclass defines "
					   + "(e.g. stats, inventory, health, custom MonoBehaviour fields).\n"
					   + "• Optionally stores its <i>parent reference</i> to survive re‑parenting.\n\n"));

			scroll.Add(H("✦ Inspector Fields"));
			scroll.Add(P(
				"<b>Keep Across Scenes</b> – enables DontDestroyOnLoad for the *entire* root.\n\n"
			  + "<b>Visible In Scenes</b> – list of scenes where the object stays active when "
			  + "Keep Across Scenes is on.\n\n"
			  + "<b>Off‑Screen Behaviour</b> – choose which components (or Rigidbody physics) to "
			  + "disable when the scene is not active.\n\n"
			  + "<b>Component ID</b> (read‑only) – stable GUID assigned automatically.\n\n"));

			scroll.Add(H("✦ Typical Workflow"));
			scroll.Add(P("1. Add a Crystal Save → Remember Component list (RememberComposite).\n"
					   + "2. Click <i>Add Remember Component…</i> to insert built‑in modules or "
					   + "your own SaveableComponent (Remember XX) subclasses.\n"
					   + "3. Use <i>Help</i> (this window) whenever you need a refresher.\n\n"));

			scroll.Add(H("✦ Gotchas"));
                       scroll.Add(P("• Only one component per GameObject may toggle <i>Keep Across Scenes</i>.\n"
                                          + "• Avoid duplicate ComponentIDs – use the <i>Generate New ID</i> button if needed.\n"
                                          + "• When you move or rename children, remember that path‑based look‑ups can break.\n"
                                          + "• Adding a Remember Componennt aka RememberComposite <i>inside a SaveablePrefab</i> is fully supported: "
                                          + "in that case the prefab’s internal <b>uniqueID</b> is used instead of the hidden "
                                          + "UniqueID component, so you <b>can</b> safely spawn or pool multiple instances "
                                          + "without ID collisions (unlike a bare RememberComposite placed in‑scene).\n"
                                          + "• Adding RememberGameObject to a SaveablePrefab instance is unnecessary – the prefab already records its GameObject state.\n\n"));

			scroll.Add(H("✦ API Snippet"));
			scroll.Add(P("Retrieve a component at runtime by ID:"));
                        scroll.Add(new Label("var sc = SaveManager.Instance.ComponentManager\n"
                                                           + "              .FindByComponentID<MyHealthComponent>(\"guid\");")
                        {
                                style =
                                {
                                        whiteSpace      = WhiteSpace.NoWrap,
                                        backgroundColor = EditorGUIUtility.isProSkin
                                                ? new Color(0.13f, 0.13f, 0.13f, 1f)
                                                : new Color(0.90f, 0.90f, 0.90f, 1f),
                                        paddingLeft     = 4,
                                        paddingRight    = 4,
                                        unityFont       = EditorStyles.miniFont
                                }
                        });

			var close = new Button(Close) { text = "Close" };
			close.style.marginTop = 6;
			scroll.Add(close);
		}
	}
}
#endif
