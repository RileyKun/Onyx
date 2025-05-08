using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Redline.Scripts.Editor {
    public class RedlineSettings : EditorWindow {
        private const string Url = "https://github.com/Redline-Team/RPM/";
        private const string Url1 = "https://arch-linux.pro/";

        public const string ProjectConfigPath = "Packages/dev.runaxr.Redline/Redline/Configs/";
        private const string BackgroundConfig = "BackgroundVideo.txt";
        private const string ProjectDownloadPath = "Packages/dev.runaxr.Redline/Redline/Assets/";

        private static GUIStyle _toolkitHeader;
        [FormerlySerializedAs("RedlineColor")] public Color redlineColor = Color.red;

        [MenuItem("Redline/Settings", false, 501)]
        private static void Init() {
            var window = (RedlineSettings)GetWindow(typeof(RedlineSettings));
            window.Show();
        }

        public static string GetAssetPath() {
            if (EditorPrefs.GetBool("Redline_onlyProject", false)) {
                return ProjectDownloadPath;
            }

            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Redline"
            );

            var assetPath = EditorPrefs.GetString("Redline_customAssetPath", defaultPath);

            // Ensure the path ends with a directory separator
            if (!assetPath.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                assetPath += Path.DirectorySeparatorChar;
            }

            Directory.CreateDirectory(assetPath);
            return assetPath;
        }

        public void OnEnable() {
            titleContent = new GUIContent("Redline Settings");
            minSize = new Vector2(400, 600);

            _toolkitHeader = new GUIStyle
            {
                normal = {
                    background = Resources.Load<Texture2D>("RedlinePMHeader"),
                    textColor = Color.white
                },
                fixedHeight = 200
            };

            // Initialize preferences
            if (!EditorPrefs.HasKey("RedlineDiscordRPC")) {
                EditorPrefs.SetBool("RedlineDiscordRPC", true);
            }

            if (!File.Exists(ProjectConfigPath + BackgroundConfig) || EditorPrefs.HasKey("Redline_background")) return;
            EditorPrefs.SetBool("Redline_background", false);
            File.WriteAllText(ProjectConfigPath + BackgroundConfig, "False");
        }

        public void OnGUI() {
            GUILayout.Box("", _toolkitHeader);
            GUILayout.Space(4);
            SetBackgroundColor();

            DisplayLinks();
            DisplayRedlineSettings();
            DisplayConsoleSettings();
            DisplayBackgroundSettings();
            DisplayAssetPathSettings();
        }

        private void SetBackgroundColor() {
            GUI.backgroundColor = new Color(
                EditorPrefs.GetFloat("RedlineColor_R"),
                EditorPrefs.GetFloat("RedlineColor_G"),
                EditorPrefs.GetFloat("RedlineColor_B"),
                EditorPrefs.GetFloat("RedlineColor_A")
            );
        }

        private static void DisplayLinks() {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Redline")) {
                Application.OpenURL(Url);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("arch-linux.pro")) {
                Application.OpenURL(Url1);
            }
            GUILayout.EndHorizontal();
        }

        private void DisplayRedlineSettings() {
            GUILayout.Space(4);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Redline Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            redlineColor = EditorGUI.ColorField(new Rect(3, 270, position.width - 6, 15), "Kit UI Color", redlineColor);
            if (EditorGUI.EndChangeCheck()) {
                SaveRedlineColor(redlineColor);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset Color")) {
                ResetRedlineColor();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        private void SaveRedlineColor(Color color) {
            EditorPrefs.SetFloat("RedlineColor_R", color.r);
            EditorPrefs.SetFloat("RedlineColor_G", color.g);
            EditorPrefs.SetFloat("RedlineColor_B", color.b);
            EditorPrefs.SetFloat("RedlineColor_A", color.a);
        }

        private void ResetRedlineColor() {
            var defaultColor = Color.red;
            SaveRedlineColor(defaultColor);
        }

        private static void DisplayConsoleSettings() {
            GUILayout.Label("Overall:");
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            var isHiddenConsole = EditorPrefs.GetBool("Redline_HideConsole");
            var enableConsoleHide = EditorGUILayout.ToggleLeft("Hide Console Errors", isHiddenConsole);

            if (enableConsoleHide != isHiddenConsole) {
                EditorPrefs.SetBool("Redline_HideConsole", enableConsoleHide);
                Debug.unityLogger.logEnabled = !enableConsoleHide;
                Debug.ClearDeveloperConsole();
            }
            GUILayout.EndHorizontal();
        }

        private static void DisplayBackgroundSettings() {
            GUILayout.Space(4);
            GUILayout.Label("Upload panel:");
            GUILayout.BeginHorizontal();
            var isBackgroundEnabled = EditorPrefs.GetBool("Redline_background", false);
            var enableBackground = EditorGUILayout.ToggleLeft("Custom background", isBackgroundEnabled);
            if (enableBackground != isBackgroundEnabled) {
                EditorPrefs.SetBool("Redline_background", enableBackground);
                File.WriteAllText(ProjectConfigPath + BackgroundConfig, enableBackground.ToString());
            }
            GUILayout.EndHorizontal();
        }

        private static void DisplayAssetPathSettings() {
            GUILayout.Space(4);
            GUILayout.Label("Import panel:");
            GUILayout.BeginHorizontal();
            var isOnlyProjectEnabled = EditorPrefs.GetBool("Redline_onlyProject", false);
            var enableOnlyProject = EditorGUILayout.ToggleLeft("Save files only in project", isOnlyProjectEnabled);
            if (enableOnlyProject != isOnlyProjectEnabled) {
                EditorPrefs.SetBool("Redline_onlyProject", enableOnlyProject);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Asset path:");
            GUILayout.BeginHorizontal();
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Redline"
            );
            var customAssetPath = EditorGUILayout.TextField(
                "",
                EditorPrefs.GetString("Redline_customAssetPath", defaultPath)
            );

            if (GUILayout.Button("Choose", GUILayout.Width(60))) {
                var path = EditorUtility.OpenFolderPanel("Asset download folder",
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Redline");
                if (!string.IsNullOrEmpty(path)) {
                    customAssetPath = path;
                }
            }

            if (GUILayout.Button("Reset", GUILayout.Width(50))) {
                customAssetPath = defaultPath;
            }

            if (EditorPrefs.GetString("Redline_customAssetPath", defaultPath) != customAssetPath) {
                EditorPrefs.SetString("Redline_customAssetPath", customAssetPath);
            }
            GUILayout.EndHorizontal();
        }
    }
}
