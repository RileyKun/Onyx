using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor
{
    public class ThemeSettings : EditorWindow
    {
        // List to store all themes
        private List<CustomTheme> allThemes = new List<CustomTheme>();

        [MenuItem("Redline/Themes/Select Themes")]
        public static void ShowWindow()
        {
            // Show the window
            GetWindow<ThemeSettings>("Theme Settings");
        }

        // Scroll position for the theme list
        private Vector2 _scrollPosition;

        // Initialize the window and load themes
        private void OnEnable()
        {
            LoadThemes();
        }

        // Method to load all themes from the file system
        private void LoadThemes()
        {
            allThemes.Clear();
            foreach (var s in Directory.GetFiles(ThemesUtility.CustomThemesPath, "*" + ThemesUtility.Enc))
            {
                var ct = ThemesUtility.GetCustomThemeFromJson(s);
                if (ct != null)
                {
                    allThemes.Add(ct);
                }
            }
        }

        // OnGUI method to draw the editor window
        private void OnGUI()
        {
            // Window title and current theme display
            GUILayout.Label("Create & Select Themes", EditorStyles.boldLabel);
            GUILayout.Label("Currently Selected: " + Path.GetFileNameWithoutExtension(ThemesUtility.CurrentTheme), EditorStyles.boldLabel);

            // Button to create a new theme
            if (GUILayout.Button("Create new Theme"))
            {
                var window = (CreateThemeWindow)GetWindow(typeof(CreateThemeWindow), false, "Create Theme");
                window.Show();
            }

            GUILayout.Label("or Select:", EditorStyles.boldLabel);

            // Group themes by type (Dark, Light, Both)
            var darkThemes = new List<CustomTheme>();
            var lightThemes = new List<CustomTheme>();
            var bothThemes = new List<CustomTheme>();

            foreach (var ct in allThemes)
            {
                switch (ct.unityTheme)
                {
                    case CustomTheme.UnityTheme.Dark:
                        darkThemes.Add(ct);
                        break;
                    case CustomTheme.UnityTheme.Light:
                        lightThemes.Add(ct);
                        break;
                    case CustomTheme.UnityTheme.Both:
                        bothThemes.Add(ct);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Begin scroll view to list themes
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DisplayThemeList("Dark & Light Themes", bothThemes);
            DisplayThemeList("Dark Themes", darkThemes);
            DisplayThemeList("Light Themes", lightThemes);

            EditorGUILayout.EndScrollView();
        }

        // Method to display a list of themes
        private static void DisplayThemeList(string title, List<CustomTheme> themes)
        {
            if (themes.Count > 0)
            {
                EditorGUILayout.LabelField("");
                EditorGUILayout.LabelField(title);
                foreach (var ct in themes)
                {
                    DisplayGUIThemeItem(ct);
                }
            }
        }

        // Method to display individual theme item UI
        private static void DisplayGUIThemeItem(CustomTheme ct)
        {
            var Name = ct.Name;

            EditorGUILayout.BeginHorizontal();

            // Load the theme when clicked
            if (GUILayout.Button(Name))
            {
                ThemesUtility.LoadUssFileForTheme(Name);
            }

            // Enable editing only if theme is not uneditable
            if (!ct.IsUnEditable && GUILayout.Button("Edit", GUILayout.Width(70)))
            {
                ThemesUtility.OpenEditTheme(ct);
            }

            // Enable deletion only if theme is not undeletable
            if (!ct.IsUnDeletable && GUILayout.Button("Delete", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Do you want to Delete " + ct.Name + "?", 
                    "Do you want to Permanently Delete the Theme " + ct.Name + " (No undo!)", 
                    "Delete", "Cancel"))
                {
                    ThemesUtility.DeleteFileWithMeta(ThemesUtility.GetPathForTheme(Name));
                    ThemesUtility.LoadUssFileForTheme("_default"); // Load default theme after deletion
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
