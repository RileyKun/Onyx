using System.IO;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor
{
    public class CreateThemeWindow : EditorWindow
    {
        private enum UnityTheme { FullDark, FullLight, Dark, Light, Both }

        private UnityTheme _unityTheme;
        private string _themeName = "EnterName";
        private string _description;

        private static readonly string CustomThemesPath = @"Packages\dev.runaxr.redline\Redline\Editor\StyleSheets\Extensions\CustomThemes\";
        private const int ButtonWidth = 200;

        [MenuItem("Redline/Themes/Create Theme")]
        public static void ShowWindow()
        {
            // Show both the custom theme window and settings window
            ThemeSettings.ShowWindow();
            GetWindow<CreateThemeWindow>("Theme Settings");
        }

        private void OnGUI()
        {
            GUILayout.Label("Create Custom Theme", EditorStyles.boldLabel);

            // Input field for theme name
            _themeName = EditorGUILayout.TextField("Theme Name", _themeName);

            // Display Preset Selection with Description
            EditorGUILayout.LabelField("Preset:");
            _unityTheme = (UnityTheme)EditorGUILayout.EnumPopup(_unityTheme);
            _description = GetPresetDescription(_unityTheme);
            EditorGUILayout.LabelField(_description);

            // Create Button (trigger the theme creation)
            if (GUILayout.Button("Create Custom Theme", GUILayout.Width(ButtonWidth)))
            {
                CreateTheme();
            }
        }

        private string GetPresetDescription(UnityTheme theme)
        {
            return theme switch
            {
                UnityTheme.FullDark => "Everything you need for a full Dark Theme.",
                UnityTheme.FullLight => "Everything you need for a full Light Theme.",
                UnityTheme.Dark => "Minimalistic Dark Theme.",
                UnityTheme.Light => "Minimalistic Light Theme.",
                UnityTheme.Both => "A theme combining both Light & Dark modes.",
                _ => string.Empty
            };
        }

        private void CreateTheme()
        {
            if (string.IsNullOrEmpty(_themeName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a valid theme name.", "OK");
                return;
            }

            string path = Path.Combine(CustomThemesPath, _themeName + ".json");

            // Check if theme already exists and prompt for overwrite confirmation
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Theme Already Exists", 
                "Do you want to overwrite the existing theme?", "Yes", "Cancel"))
            {
                return;
            }

            try
            {
                // Fetch the base theme based on the preset selection
                CustomTheme customTheme = FetchTheme(_unityTheme.ToString(), _themeName);

                // Save and open the theme for editing
                ThemesUtility.SaveJsonFileForTheme(customTheme);
                ThemesUtility.OpenEditTheme(customTheme);

                // Close the window after saving and opening the theme
                Close();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private static CustomTheme FetchTheme(string presetName, string themeName)
        {
            // Get the base theme from preset and update its name
            var customTheme = ThemesUtility.GetCustomThemeFromJson(Path.Combine(ThemesUtility.PresetsPath, presetName + ".json"));
            customTheme.Name = themeName;
            return customTheme;
        }
    }
}
