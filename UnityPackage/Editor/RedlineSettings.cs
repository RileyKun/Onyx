using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Redline.Editor.DiscordRPC;
using Redline.Editor.VPM;
using Redline.Runtime.DiscordRPC;

namespace Redline.Editor
{
    public class RedlineSettings : EditorWindow {
        private const string Url = "https://github.com/Redline-Team/RPM/";
        private const string Url1 = "https://arch-linux.pro/";

        public const string ProjectConfigPath = "Packages/dev.redline-team.rpm/Configs/";
        private const string ProjectDownloadPath = "Packages/dev.redline-team.rpm/Resources/";

        private static GUIStyle _toolkitHeader;
        
        // Cache paths to avoid accessing EditorPrefs from background threads
        private static string _cachedAssetPath;
        private static string _cachedRepositoriesPath;
        private static bool _pathsInitialized;

        [MenuItem("Redline/Settings", false, 501)]
        public static void Init() {
            var window = (RedlineSettings)GetWindow(typeof(RedlineSettings));
            window.Show();
        }

        public static void InitializePaths()
        {
            if (!_pathsInitialized)
            {
                _cachedAssetPath = GetAssetPathInternal();
                _cachedRepositoriesPath = Path.Combine(_cachedAssetPath, "Repos");
                _pathsInitialized = true;
            }
        }

        private static string GetAssetPathInternal()
        {
            if (EditorPrefs.GetBool("Redline_onlyProject", false))
            {
                return ProjectDownloadPath;
            }

            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Redline"
            );

            var assetPath = EditorPrefs.GetString("RedlineDirectory", defaultPath);

            // Ensure the path ends with a directory separator
            if (!assetPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                assetPath += Path.DirectorySeparatorChar;
            }

            Directory.CreateDirectory(assetPath);
            return assetPath;
        }

        public static string GetAssetPath()
        {
            if (!_pathsInitialized)
            {
                InitializePaths();
            }
            return _cachedAssetPath;
        }

        public static string GetRepositoriesPath()
        {
            if (!_pathsInitialized)
            {
                InitializePaths();
            }
            return _cachedRepositoriesPath;
        }

        public void OnEnable() {
            titleContent = new GUIContent("Redline Settings");
            minSize = new Vector2(400, 600);

            _toolkitHeader = new GUIStyle
            {
                normal = {
                    background = Resources.Load<Texture2D>("RedlinePMHeader"),
                    textColor = Color.white
                }
                // No fixed height - will be calculated dynamically to maintain aspect ratio
            };

            // Initialize preferences
            if (!EditorPrefs.HasKey("RedlineDiscordRPC")) {
                EditorPrefs.SetBool("RedlineDiscordRPC", true);
            }

            // Initialize paths
            InitializePaths();

            // Copy default repositories to user's directory
            VPMRepository.CopyDefaultRepositories();
        }

        public void OnGUI() {
            // Draw the banner with dynamic height to maintain aspect ratio
            if (_toolkitHeader != null && _toolkitHeader.normal.background != null)
            {
                Texture2D headerTexture = _toolkitHeader.normal.background;
                // Original aspect ratio is 1024:217
                float aspectRatio = 1024f / 217f;
                // Calculate height based on current window width to maintain aspect ratio
                float width = EditorGUIUtility.currentViewWidth;
                float height = width / aspectRatio;
                
                // Draw the banner with calculated height
                Rect bannerRect = GUILayoutUtility.GetRect(width, height);
                GUI.Box(bannerRect, "", _toolkitHeader);
            }
            GUILayout.Space(4);

            DisplayLinks();
            DisplayRedlineSettings();
            DisplayConsoleSettings();
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
            
            // Compacted Overflow Fix Toggle
            GUILayout.BeginHorizontal();
            var isCompactedOverflowEnabled = EditorPrefs.GetBool("Redline_CompactedOverflowFix", true);
            var enableCompactedOverflow = EditorGUILayout.ToggleLeft("Compacted Overflow Fix", isCompactedOverflowEnabled);
            if (enableCompactedOverflow != isCompactedOverflowEnabled) {
                EditorPrefs.SetBool("Redline_CompactedOverflowFix", enableCompactedOverflow);
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("When enabled, repositories in the Community tab will be compacted into a dropdown if there are more than 4 rows.", MessageType.Info);
            
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
            
            // Only show Discord RPC settings if it's enabled
            if (enableDiscordRpc) {
                EditorGUILayout.Space(5);
                
                DrawDiscordRPCSettings();
                
                EditorGUILayout.Space(5);
                
                // Buttons for resetting and refreshing
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(120))) {
                    RpcStateInfo.ResetToDefaults();
                    RefreshDiscordRPC();
                }
                
                if (GUILayout.Button("Refresh Discord RPC", GUILayout.Width(150))) {
                    RefreshDiscordRPC();
                }
                GUILayout.EndHorizontal();
            }
            
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

        private void DisplayAssetPathSettings() {
            GUILayout.Space(4);
            GUILayout.Label("Redline Workspace:");
            GUILayout.BeginHorizontal();
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Redline"
            );
            var customAssetPath = EditorGUILayout.TextField(
                "",
                EditorPrefs.GetString("RedlineDirectory", defaultPath)
            );

            if (GUILayout.Button("Choose", GUILayout.Width(60))) {
                var path = EditorUtility.OpenFolderPanel("Select Redline Workspace",
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Redline");
                if (!string.IsNullOrEmpty(path)) {
                    customAssetPath = path;
                }
            }

            if (GUILayout.Button("Reset", GUILayout.Width(50))) {
                customAssetPath = defaultPath;
            }

            if (EditorPrefs.GetString("RedlineDirectory", defaultPath) != customAssetPath) {
                EditorPrefs.SetString("RedlineDirectory", customAssetPath);
            }
            GUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Refreshes the Discord Rich Presence by calling UpdateDrpc method
        /// </summary>
        private static void RefreshDiscordRPC() {
            if (EditorPrefs.GetBool("RedlineDiscordRPC", true)) {
                // Use reflection to call the private UpdateDrpc method
                var method = typeof(RedlineDiscordRPC).GetMethod("UpdateDrpc", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (method != null) {
                    method.Invoke(null, null);
                    Debug.Log("[Redline] Discord RPC refreshed");
                }
            }
        }

        private void DrawDiscordRPCSettings() {
            EditorGUILayout.LabelField("Discord RPC Settings", EditorStyles.boldLabel);
            
            // Editmode state
            string editmodeState = EditorGUILayout.TextField("Editmode State", RpcStateInfo.StateName(RpcState.Editmode));
            if (editmodeState != RpcStateInfo.StateName(RpcState.Editmode)) {
                EditorPrefs.SetString(RpcStateInfo.EditmodeStateKey, editmodeState);
            }
            
            // Playmode state
            string playmodeState = EditorGUILayout.TextField("Playmode State", RpcStateInfo.StateName(RpcState.Playmode));
            if (playmodeState != RpcStateInfo.StateName(RpcState.Playmode)) {
                EditorPrefs.SetString(RpcStateInfo.PlaymodeStateKey, playmodeState);
            }
            
            // Uploadpanel state
            string uploadpanelState = EditorGUILayout.TextField("Uploadpanel State", RpcStateInfo.StateName(RpcState.Uploadpanel));
            if (uploadpanelState != RpcStateInfo.StateName(RpcState.Uploadpanel)) {
                EditorPrefs.SetString(RpcStateInfo.UploadpanelStateKey, uploadpanelState);
            }
            
            // Idle state
            string idleState = EditorGUILayout.TextField("Idle State", RpcStateInfo.StateName(RpcState.Idle));
            if (idleState != RpcStateInfo.StateName(RpcState.Idle)) {
                EditorPrefs.SetString(RpcStateInfo.IdleStateKey, idleState);
            }
            
            // Idle timer
            int idleTimer = EditorGUILayout.IntField("Idle Timer (minutes)", RpcStateInfo.GetIdleTimerMinutes());
            if (idleTimer != RpcStateInfo.GetIdleTimerMinutes()) {
                EditorPrefs.SetInt(RpcStateInfo.IdleTimerKey, idleTimer);
                RedlineDiscordRPC.UpdateIdleTimerInterval();
            }
            
            // Reset to defaults button
            if (GUILayout.Button("Reset to Defaults")) {
                RpcStateInfo.ResetToDefaults();
            }
        }
    }
}
