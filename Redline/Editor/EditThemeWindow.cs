using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor
{
    public class EditThemeWindow : EditorWindow
    {
        public static CustomTheme Ct;
        private string Name;
        private Vector2 _scrollPosition;
        private List<Color> _simpleColors = new List<Color>();
        private List<Color> _lastSimpleColors = new List<Color>();

        private enum CustomView { Simple, Advanced }
        private CustomView _customView;
        private bool _rhold, _strgHold;

        private void OnDestroy()
        {
            Ct = null;
        }

        private void Awake()
        {
            _simpleColors = CreateAverageColors();
            _lastSimpleColors = CreateAverageColors();
            Name = Ct.Name;
        }

        private void OnGUI()
        {
            if (Ct == null)
            {
                Close();
                return;
            }

            HandleEventInput();

            // Ask to regenerate theme if necessary
            if (_rhold && _strgHold && EditorUtility.DisplayDialog(
                "Do you want to regenerate this Theme? (Make a Clone first!)",
                "Regenerating is helpful when the Theme was made with an older version of the Plugin (but you might lose small amounts of data)",
                "Continue", "Cancel"))
            {
                RegenerateTheme();
            }

            // Display UI for theme editing
            DrawThemeEditingUI();
        }

        private void HandleEventInput()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.R) _rhold = true;
                if (e.keyCode == KeyCode.LeftControl) _strgHold = true;
            }
            else if (e.type == EventType.KeyUp)
            {
                if (e.keyCode == KeyCode.R) _rhold = false;
                if (e.keyCode == KeyCode.LeftControl) _strgHold = false;
            }
        }

        private void RegenerateTheme()
        {
            Ct.Items = new List<CustomTheme.UIItem>();
            for (var i = 0; i < 6; i++)
            {
                var colorItems = ThemesUtility.GetColorListByInt(i)
                    .Select(s => new CustomTheme.UIItem { Name = s, Color = _simpleColors[i] })
                    .ToList();
                Ct.Items.AddRange(colorItems);
            }
        }

        private void DrawThemeEditingUI()
        {
            EditorGUILayout.LabelField("\n");

            // Theme name field
            Name = EditorGUILayout.TextField(Name);
            EditorGUILayout.LabelField("\n");

            // View selection
            _customView = (CustomView)EditorGUILayout.EnumPopup(_customView, GUILayout.Width(100));

            // Advanced or Simple view
            if (_customView == CustomView.Advanced)
            {
                DrawAdvancedView();
            }
            else
            {
                DrawSimpleView();
            }

            // Unity Theme selection
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Unity Theme:");
            Ct.unityTheme = (CustomTheme.UnityTheme)EditorGUILayout.EnumPopup(Ct.unityTheme, GUILayout.Width(100));

            EditorGUILayout.LabelField("");

            // Save and Clone buttons
            DrawSaveAndCloneButtons();
        }

        private void DrawAdvancedView()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            var ctItemsClone = new List<CustomTheme.UIItem>(Ct.Items);
            foreach (var item in ctItemsClone)
            {
                DrawUIItemInAdvancedView(item);
            }
            EditorGUILayout.EndScrollView();

            // Add/remove UI items
            DrawItemAddRemoveButtons();
        }

        private void DrawUIItemInAdvancedView(CustomTheme.UIItem item)
        {
            EditorGUILayout.BeginHorizontal();
            item.Name = EditorGUILayout.TextField(item.Name, GUILayout.Width(200));
            if (GUILayout.Button("Del", GUILayout.Width(50)))
            {
                Ct.Items.Remove(item);
            }
            EditorGUILayout.EndHorizontal();
            item.Color = EditorGUILayout.ColorField(item.Color, GUILayout.Width(200));
        }

        private void DrawItemAddRemoveButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add", GUILayout.Width(200)))
            {
                Ct.Items.Add(new CustomTheme.UIItem { Name = "Enter Name" });
            }
            if (Ct.Items.Count > 0 && GUILayout.Button("Remove", GUILayout.Width(200)))
            {
                Ct.Items.RemoveAt(Ct.Items.Count - 1);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSimpleView()
        {
            GUILayout.Label("Base Color:", EditorStyles.boldLabel);
            _simpleColors[0] = EditorGUILayout.ColorField(_simpleColors[0]);
            GUILayout.Label("Accent Color:", EditorStyles.boldLabel);
            _simpleColors[1] = EditorGUILayout.ColorField(_simpleColors[1]);
            GUILayout.Label("Secondary Base Color:", EditorStyles.boldLabel);
            _simpleColors[2] = EditorGUILayout.ColorField(_simpleColors[2]);
            GUILayout.Label("Tab Color:", EditorStyles.boldLabel);
            _simpleColors[3] = EditorGUILayout.ColorField(_simpleColors[3]);
            GUILayout.Label("Command Bar Color:", EditorStyles.boldLabel);
            _simpleColors[4] = EditorGUILayout.ColorField(_simpleColors[4]);
            GUILayout.Label("Additional Color:", EditorStyles.boldLabel);
            _simpleColors[5] = EditorGUILayout.ColorField(_simpleColors[5]);

            // Check for color changes and apply them
            for (var i = 0; i < _simpleColors.Count; i++)
            {
                if (_simpleColors[i] != _lastSimpleColors[i])
                {
                    EditColor(i, _simpleColors[i]);
                }
            }
        }

        private void DrawSaveAndCloneButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // Save button
            if (GUILayout.Button("Save", GUILayout.Width(200)))
            {
                if (Ct.Name != Name)
                {
                    ThemesUtility.DeleteFileWithMeta(ThemesUtility.GetPathForTheme(Ct.Name));
                }
                Ct.Name = Name;
                ThemesUtility.SaveJsonFileForTheme(Ct);
            }

            // Clone button
            if (GUILayout.Button("Clone", GUILayout.Width(200)))
            {
                Ct.Name = Name + " - c";
                ThemesUtility.SaveJsonFileForTheme(Ct);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static CustomTheme.UIItem GetItemByName(string name)
        {
            return Ct.Items.FirstOrDefault(item => item.Name == name);
        }

        private List<Color> CreateAverageColors()
        {
            var colors = new List<Color>();

            for (var i = 0; i < 6; i++)
            {
                var colorObjects = ThemesUtility.GetColorListByInt(i);
                var allColors = colorObjects
                    .Select(s => GetItemByName(s))
                    .Where(item => item != null)
                    .Select(item => item.Color)
                    .ToList();

                colors.Add(allColors.Count > 0 ? GetAverage(allColors) : ThemesUtility.HtmlToRgb("#9A7B6E"));
            }

            return colors;
        }

        private void EditColor(int i, Color newColor)
        {
            var itemsToEdit = ThemesUtility.GetColorListByInt(i)
                .Select(GetItemByName)
                .Where(item => item != null)
                .ToList();

            foreach (var item in itemsToEdit)
            {
                item.Color = newColor;
            }

            _lastSimpleColors[i] = _simpleColors[i];
        }

        private static Color GetAverage(List<Color> colors)
        {
            float r = 0, g = 0, b = 0;

            foreach (var color in colors)
            {
                r += color.r;
                g += color.g;
                b += color.b;
            }

            return new Color(r / colors.Count, g / colors.Count, b / colors.Count);
        }
    }
}
