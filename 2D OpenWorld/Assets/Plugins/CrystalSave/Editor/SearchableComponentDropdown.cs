using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// A searchable dropdown window for selecting Remember Components.
	/// Provides modern search/filter functionality.
	/// </summary>
	public class SearchableComponentDropdown : EditorWindow
	{
		private string[] _typeNames;
		private string[] _displayNames;
		private Dictionary<Type, Texture> _iconCache;
		private Action<int> _onSelect;
		private Func<string, bool> _isAlreadyAdded;
		
		private string _searchText = "";
		private Vector2 _scrollPos;
		private List<int> _filteredIndices = new List<int>();
		private int _selectedIndex = -1;
		private bool _focusSearchField = true;
		
		private const float WINDOW_WIDTH = 350f;
		private const float WINDOW_MAX_HEIGHT = 400f;
		private const float ITEM_HEIGHT = 20f;
		private const float ICON_SIZE = 16f;
		
		private static SearchableComponentDropdown _currentWindow;
		
		// Theme colors - automatically adjust based on Unity's editor theme
		private static Color BackgroundColor => EditorGUIUtility.isProSkin 
			? new Color(0.2f, 0.2f, 0.2f, 1f)   // Dark theme
			: new Color(0.76f, 0.76f, 0.76f, 1f); // Light theme
		
		private static Color SelectionColor => EditorGUIUtility.isProSkin 
			? new Color(0.24f, 0.48f, 0.9f, 0.5f)  // Dark theme - blue highlight
			: new Color(0.22f, 0.45f, 0.87f, 0.4f); // Light theme - lighter blue
		
		private static Color DisabledTextColor => EditorGUIUtility.isProSkin 
			? new Color(0.6f, 0.6f, 0.6f, 1f)    // Dark theme
			: new Color(0.5f, 0.5f, 0.5f, 1f);   // Light theme
		
		private static Color HighlightTextColor => EditorGUIUtility.isProSkin 
			? new Color(1f, 0.8f, 0.2f, 1f)      // Dark theme - golden yellow
			: new Color(0.9f, 0.5f, 0f, 1f);     // Light theme - darker orange
		
		public static void Show(
			Rect buttonRect, 
			string[] typeNames, 
			string[] displayNames,
			Dictionary<Type, Texture> iconCache,
			Action<int> onSelect,
			Func<string, bool> isAlreadyAdded = null)
		{
			// Close any existing window
			if (_currentWindow != null)
			{
				_currentWindow.Close();
			}
			
			var window = CreateInstance<SearchableComponentDropdown>();
			window._typeNames = typeNames;
			window._displayNames = displayNames;
			window._iconCache = iconCache;
			window._onSelect = onSelect;
			window._isAlreadyAdded = isAlreadyAdded ?? (fq => false);
			
			// Position window below the button
			Vector2 windowPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.yMax));
			float windowHeight = Mathf.Min(WINDOW_MAX_HEIGHT, (displayNames.Length * ITEM_HEIGHT) + 50f);
			
			window.position = new Rect(windowPos.x, windowPos.y, WINDOW_WIDTH, windowHeight);
			window.ShowPopup();
			window.UpdateFilteredList();
			window.Focus();
			
			_currentWindow = window;
		}
		
		private void OnEnable()
		{
			wantsMouseMove = true;
			wantsMouseEnterLeaveWindow = true;
			EditorApplication.update += CheckForCloseConditions;
		}
		
		private void OnDisable()
		{
			EditorApplication.update -= CheckForCloseConditions;
			
			if (_currentWindow == this)
			{
				_currentWindow = null;
			}
		}
		
		private void CheckForCloseConditions()
		{
			// Check if the window should be closed (e.g., lost focus)
			if (this == null || !focusedWindow || focusedWindow != this)
			{
				// Don't close immediately, give a frame delay to handle clicks
				return;
			}
		}
		
		private void OnGUI()
		{
			// Handle events that should close the window first
			Event e = Event.current;
			
			// Handle Escape key
			if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
			{
				Close();
				e.Use();
				return;
			}
			
			// Close on click outside - check using window-local coordinates
			if (e.type == EventType.MouseDown)
			{
				Vector2 mousePos = e.mousePosition;
				Rect windowRect = new Rect(0, 0, position.width, position.height);
				
				if (!windowRect.Contains(mousePos))
				{
					Close();
					GUIUtility.ExitGUI();
					return;
				}
			}
			
			// Draw background (adapts to Unity theme)
			EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BackgroundColor);
			
			// Handle keyboard navigation
			HandleKeyboard();
			
			GUILayout.BeginVertical();
			
			// Search field
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUI.SetNextControlName("SearchField");
			string newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
			
			if (newSearch != _searchText)
			{
				_searchText = newSearch;
				UpdateFilteredList();
				_selectedIndex = _filteredIndices.Count > 0 ? 0 : -1;
			}
			
			if (GUILayout.Button("", EditorStyles.toolbarButton, GUILayout.Width(20)))
			{
				_searchText = "";
				UpdateFilteredList();
				GUI.FocusControl("SearchField");
			}
			
			EditorGUILayout.EndHorizontal();
			
			// Auto-focus search field on first frame
			if (_focusSearchField)
			{
				EditorGUI.FocusTextInControl("SearchField");
				_focusSearchField = false;
			}
			
			// Results count
			if (!string.IsNullOrEmpty(_searchText))
			{
				EditorGUILayout.LabelField($"Found {_filteredIndices.Count} component(s)", EditorStyles.miniLabel);
			}
			
			// Scrollable list
			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
			
			for (int i = 0; i < _filteredIndices.Count; i++)
			{
				int actualIndex = _filteredIndices[i];
				string displayName = _displayNames[actualIndex];
				string typeName = _typeNames[actualIndex];
				bool isAdded = _isAlreadyAdded(typeName);
				
				// Highlight selected item (adapts to Unity theme)
				Rect itemRect = EditorGUILayout.GetControlRect(false, ITEM_HEIGHT);
				if (i == _selectedIndex)
				{
					EditorGUI.DrawRect(itemRect, SelectionColor);
				}
				
				// Draw item
				Rect contentRect = new Rect(itemRect.x + 4, itemRect.y, itemRect.width - 8, itemRect.height);
				
				// Icon
				Type componentType = Type.GetType(typeName);
				if (componentType != null && _iconCache != null && _iconCache.TryGetValue(componentType, out var icon) && icon != null)
				{
					Rect iconRect = new Rect(contentRect.x, contentRect.y + (contentRect.height - ICON_SIZE) / 2, ICON_SIZE, ICON_SIZE);
					GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
					contentRect.x += ICON_SIZE + 4;
					contentRect.width -= ICON_SIZE + 4;
				}
				
				// Display name
				GUIStyle labelStyle = isAdded ? EditorStyles.label : EditorStyles.label;
				Color originalColor = GUI.color;
				
				if (isAdded)
				{
					GUI.color = DisabledTextColor;
					GUI.Label(contentRect, displayName + " (Already Added)", labelStyle);
				}
				else
				{
					// Highlight search matches
					if (!string.IsNullOrEmpty(_searchText))
					{
						DrawHighlightedLabel(contentRect, displayName, _searchText);
					}
					else
					{
						GUI.Label(contentRect, displayName, labelStyle);
					}
				}
				
				GUI.color = originalColor;
				
				// Handle click
				if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
				{
					if (!isAdded)
					{
						SelectItem(actualIndex);
					}
					Event.current.Use();
				}
				
				// Update selected index on hover
				if (Event.current.type == EventType.MouseMove && itemRect.Contains(Event.current.mousePosition))
				{
					_selectedIndex = i;
					Repaint();
				}
			}
			
			EditorGUILayout.EndScrollView();
			
			// No results message
			if (_filteredIndices.Count == 0)
			{
				EditorGUILayout.LabelField("No components found", EditorStyles.centeredGreyMiniLabel);
			}
			
			GUILayout.EndVertical();
		}
		
		private void HandleKeyboard()
		{
			if (Event.current.type != EventType.KeyDown)
				return;
			
			switch (Event.current.keyCode)
			{
				case KeyCode.Escape:
					Close();
					Event.current.Use();
					break;
				
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					if (_selectedIndex >= 0 && _selectedIndex < _filteredIndices.Count)
					{
						int actualIndex = _filteredIndices[_selectedIndex];
						string typeName = _typeNames[actualIndex];
						if (!_isAlreadyAdded(typeName))
						{
							SelectItem(actualIndex);
						}
					}
					Event.current.Use();
					break;
				
				case KeyCode.DownArrow:
					if (_filteredIndices.Count > 0)
					{
						_selectedIndex = (_selectedIndex + 1) % _filteredIndices.Count;
						ScrollToSelected();
						Repaint();
					}
					Event.current.Use();
					break;
				
				case KeyCode.UpArrow:
					if (_filteredIndices.Count > 0)
					{
						_selectedIndex = (_selectedIndex - 1 + _filteredIndices.Count) % _filteredIndices.Count;
						ScrollToSelected();
						Repaint();
					}
					Event.current.Use();
					break;
			}
		}
		
		private void ScrollToSelected()
		{
			if (_selectedIndex < 0 || _selectedIndex >= _filteredIndices.Count)
				return;
			
			float itemY = _selectedIndex * ITEM_HEIGHT;
			float viewHeight = position.height - 60f; // Account for search bar and padding
			
			if (itemY < _scrollPos.y)
			{
				_scrollPos.y = itemY;
			}
			else if (itemY + ITEM_HEIGHT > _scrollPos.y + viewHeight)
			{
				_scrollPos.y = itemY + ITEM_HEIGHT - viewHeight;
			}
		}
		
		private void UpdateFilteredList()
		{
			_filteredIndices.Clear();
			
			if (string.IsNullOrEmpty(_searchText))
			{
				// Show all items
				for (int i = 0; i < _displayNames.Length; i++)
				{
					_filteredIndices.Add(i);
				}
			}
			else
			{
				// Filter by search text (case-insensitive)
				string searchLower = _searchText.ToLowerInvariant();
				
				for (int i = 0; i < _displayNames.Length; i++)
				{
					string displayLower = _displayNames[i].ToLowerInvariant();
					if (displayLower.Contains(searchLower))
					{
						_filteredIndices.Add(i);
					}
				}
			}
		}
		
		private void DrawHighlightedLabel(Rect rect, string text, string searchText)
		{
			int startIndex = text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
			
			if (startIndex == -1)
			{
				GUI.Label(rect, text);
				return;
			}
			
			GUIStyle normalStyle = EditorStyles.label;
			GUIStyle highlightStyle = new GUIStyle(normalStyle);
			highlightStyle.normal.textColor = HighlightTextColor;
			highlightStyle.fontStyle = FontStyle.Bold;
			
			string before = text.Substring(0, startIndex);
			string match = text.Substring(startIndex, searchText.Length);
			string after = text.Substring(startIndex + searchText.Length);
			
			float xOffset = 0;
			
			// Draw before text
			if (!string.IsNullOrEmpty(before))
			{
				GUI.Label(new Rect(rect.x + xOffset, rect.y, rect.width, rect.height), before, normalStyle);
				xOffset += normalStyle.CalcSize(new GUIContent(before)).x;
			}
			
			// Draw highlighted match
			GUI.Label(new Rect(rect.x + xOffset, rect.y, rect.width, rect.height), match, highlightStyle);
			xOffset += highlightStyle.CalcSize(new GUIContent(match)).x;
			
			// Draw after text
			if (!string.IsNullOrEmpty(after))
			{
				GUI.Label(new Rect(rect.x + xOffset, rect.y, rect.width, rect.height), after, normalStyle);
			}
		}
		
		private void SelectItem(int index)
		{
			_onSelect?.Invoke(index);
			Close();
		}
		
		private void OnLostFocus()
		{
			// Close when window loses focus
			Close();
		}
	}
}
