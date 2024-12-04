#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
    [InitializeOnLoad]
    public class Sceneautosave : EditorWindow {
        private const float DefaultSaveInterval = 60f; // Default auto-save interval (in seconds)
        private static float _saveInterval; // Current save interval (in seconds)
        private static float _timeLeft;
        private static Vector2 _changeLogScroll;

        static Sceneautosave() {
            // Load the previously saved interval (or default if not set)
            _saveInterval = EditorPrefs.GetFloat("SceneAutoSaveInterval", DefaultSaveInterval);

            // Check the splash screen display status
            EditorApplication.update -= DoSplashScreen;
            EditorApplication.update += DoSplashScreen;
        }

        private static void DoSplashScreen() {
            EditorApplication.update -= DoSplashScreen;
            if (!EditorPrefs.HasKey("ShowSplashScreen")) {
                EditorPrefs.SetBool("ShowSplashScreen", true);
            }

            if (EditorPrefs.GetBool("ShowSplashScreen")) {
                OpenSplashScreen();
            }
        }

        private static void OpenSplashScreen() {
            GetWindowWithRect<Sceneautosave>(new Rect(0, 0, 400, 200), true);
        }

        [MenuItem("Redline/Scene AutoSave", false, 500)]
        private static void Init() {
            var window = (Sceneautosave)GetWindow(typeof(Sceneautosave));
            window.Show();
        }

        public void OnEnable() {
            titleContent = new GUIContent("Auto Save");
            minSize = new Vector2(400, 200);
        }

        public void OnGUI() {
            // Display the current interval and allow users to change it
            EditorGUILayout.LabelField("Auto-Save Interval (seconds):");
            _saveInterval = EditorGUILayout.FloatField("Interval:", _saveInterval);

            // Ensure the interval is a positive number (prevent negative or zero values)
            if (_saveInterval <= 0) {
                _saveInterval = DefaultSaveInterval; // Reset to default if the value is invalid
            }

            // Save the updated interval to EditorPrefs for future use
            EditorPrefs.SetFloat("SceneAutoSaveInterval", _saveInterval);

            // Display the time left for the next auto-save
            var timeToSave = Mathf.CeilToInt(_timeLeft - EditorApplication.timeSinceStartup);
            EditorGUILayout.LabelField("Time to next save:", $"{timeToSave} seconds");

            Repaint();

            // Check if it's time to save the scene
            if (EditorApplication.timeSinceStartup > _timeLeft) {
                AutoSaveScene();
                _timeLeft = EditorApplication.timeSinceStartup + _saveInterval;
            }

            DrawExternalLinks();

            // Toggle AutoSave feature visibility
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorPrefs.SetBool("ShowSplashScreen", GUILayout.Toggle(EditorPrefs.GetBool("ShowSplashScreen"), "Toggle AutoSave"));
            GUILayout.EndHorizontal();
        }

        private void AutoSaveScene() {
            // Auto-save logic: Prepend "AutoSave_" to the current scene name and save
            var scenePath = EditorApplication.currentScene;
            var sceneName = System.IO.Path.GetFileName(scenePath);
            var autoSavePath = scenePath.Replace(sceneName, "AutoSave_" + sceneName);
            EditorApplication.SaveScene(autoSavePath, true);

            Debug.Log($"Scene auto-saved: {autoSavePath}");
        }

        private void DrawExternalLinks() {
            // External links with color-coded buttons
            GUILayout.BeginHorizontal();
            CreateLinkButton("Trigon", "https://trigon.systems/");
            CreateLinkButton("GitHub", "https://github.com/Redline-Team/RPM");
            CreateLinkButton("C0deRa1n", "https://c0dera.in/");
            CreateLinkButton("Support", "https://github.com/Redline-Team/RPM/issues");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            CreateLinkButton("Status", "https://status.trigon.systems");
            GUILayout.EndHorizontal();
        }

        private void CreateLinkButton(string label, string url) {
            GUI.backgroundColor = GetButtonColor(label);
            if (GUILayout.Button(label)) {
                Application.OpenURL(url);
            }
        }

        private Color GetButtonColor(string label) {
            return label switch {
                "Trigon" => Color.cyan,
                "GitHub" => Color.red,
                "C0deRa1n" => Color.yellow,
                "Support" => Color.green,
                "Status" => Color.gray,
                _ => Color.white
            };
        }
    }
}
#endif
