using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Redline.Scripts.Editor {
    public class RedlineSettings : EditorWindow {
        private const string Url = "https://github.com/Redline-Team/RPM/";
        private const string Url1 = "https://arch-linux.pro/";

        public const string ProjectConfigPath = "Packages/dev.redline-team.rpm/Redline/Configs/";
        private const string BackgroundConfig = "BackgroundVideo.txt";
        private const string ProjectDownloadPath = "Packages/dev.redline-team.rpm/Redline/Assets/";

        private static GUIStyle _toolkitHeader;

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

            DisplayLinks();
            DisplayRedlineSettings();
            DisplayConsoleSettings();
            DisplayBackgroundSettings();
            DisplayAssetPathSettings();
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
            
            // Discord RPC Toggle
            GUILayout.BeginHorizontal();
            var isDiscordRpcEnabled = EditorPrefs.GetBool("RedlineDiscordRPC", true);
            var enableDiscordRpc = EditorGUILayout.ToggleLeft("Enable Discord Rich Presence", isDiscordRpcEnabled);
            if (enableDiscordRpc != isDiscordRpcEnabled) {
                EditorPrefs.SetBool("RedlineDiscordRPC", enableDiscordRpc);
                EditorUtility.DisplayDialog("Discord RPC Setting Changed", 
                    "The Discord Rich Presence setting has been changed. Please restart Unity for this change to take effect.", 
                    "OK");
            }
            GUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck()) {
                // Handle changes if needed
            }
            
            EditorGUILayout.EndVertical();
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
