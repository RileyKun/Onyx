using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Redline.Scripts.Editor.DiscordRPC.Editor;
using Redline.Editor.VPM;

namespace Redline.Scripts.Editor {
    public class RedlineSettings : EditorWindow {
        private const string Url = "https://github.com/Redline-Team/RPM/";
        private const string Url1 = "https://arch-linux.pro/";

        public const string ProjectConfigPath = "Packages/dev.redline-team.rpm/Redline/Configs/";
        private const string ProjectDownloadPath = "Packages/dev.redline-team.rpm/Redline/Assets/";

        private static GUIStyle _toolkitHeader;

        [MenuItem("Redline/Settings", false, 501)]
        public static void Init() {
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

            var assetPath = EditorPrefs.GetString("RedlineDirectory", defaultPath);

            // Ensure the path ends with a directory separator
            if (!assetPath.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                assetPath += Path.DirectorySeparatorChar;
            }

            Directory.CreateDirectory(assetPath);
            return assetPath;
        }

        /// <summary>
        /// Gets the path where VPM repositories should be stored
        /// </summary>
        public static string GetRepositoriesPath() {
            return Path.Combine(GetAssetPath(), "Repos");
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
                
                // Idle timer (always visible)
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Idle Timer (minutes):", GUILayout.Width(120));
                var idleTimer = EditorGUILayout.IntField(
                    EditorPrefs.GetInt(DiscordRPC.RpcStateInfo.IdleTimerKey, 5)
                );
                
                // Ensure the timer is at least 1 minute
                if (idleTimer < 1) idleTimer = 1;
                
                if (EditorPrefs.GetInt(DiscordRPC.RpcStateInfo.IdleTimerKey) != idleTimer) {
                    EditorPrefs.SetInt(DiscordRPC.RpcStateInfo.IdleTimerKey, idleTimer);
                    
                    // Update the idle timer interval
                    DiscordRPC.Editor.RedlineDiscordRPC.UpdateIdleTimerInterval();
                }
                GUILayout.EndHorizontal();
                
                // Store the foldout state in EditorPrefs
                bool showStateNames = EditorPrefs.GetBool("RedlineDiscordRPC_ShowStateNames", false);
                bool newShowStateNames = EditorGUILayout.Foldout(showStateNames, "Discord RPC State Names", true);
                
                if (newShowStateNames != showStateNames) {
                    EditorPrefs.SetBool("RedlineDiscordRPC_ShowStateNames", newShowStateNames);
                }
                
                // Show state name fields if foldout is expanded
                if (newShowStateNames) {
                    EditorGUI.indentLevel++;
                    
                    // Editmode state
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Editmode State:", GUILayout.Width(120));
                    var editmodeState = EditorGUILayout.TextField(
                        EditorPrefs.GetString(DiscordRPC.RpcStateInfo.EditmodeStateKey, "Modifying")
                    );
                    if (EditorPrefs.GetString(DiscordRPC.RpcStateInfo.EditmodeStateKey) != editmodeState) {
                        EditorPrefs.SetString(DiscordRPC.RpcStateInfo.EditmodeStateKey, editmodeState);
                    }
                    GUILayout.EndHorizontal();
                    
                    // Playmode state
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Playmode State:", GUILayout.Width(120));
                    var playmodeState = EditorGUILayout.TextField(
                        EditorPrefs.GetString(DiscordRPC.RpcStateInfo.PlaymodeStateKey, "Testing")
                    );
                    if (EditorPrefs.GetString(DiscordRPC.RpcStateInfo.PlaymodeStateKey) != playmodeState) {
                        EditorPrefs.SetString(DiscordRPC.RpcStateInfo.PlaymodeStateKey, playmodeState);
                    }
                    GUILayout.EndHorizontal();
                    
                    // Uploadpanel state
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Uploadpanel State:", GUILayout.Width(120));
                    var uploadpanelState = EditorGUILayout.TextField(
                        EditorPrefs.GetString(DiscordRPC.RpcStateInfo.UploadpanelStateKey, "Updating content")
                    );
                    if (EditorPrefs.GetString(DiscordRPC.RpcStateInfo.UploadpanelStateKey) != uploadpanelState) {
                        EditorPrefs.SetString(DiscordRPC.RpcStateInfo.UploadpanelStateKey, uploadpanelState);
                    }
                    GUILayout.EndHorizontal();
                    
                    // Idle state
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Idle State:", GUILayout.Width(120));
                    var idleState = EditorGUILayout.TextField(
                        EditorPrefs.GetString(DiscordRPC.RpcStateInfo.IdleStateKey, "Idle")
                    );
                    if (EditorPrefs.GetString(DiscordRPC.RpcStateInfo.IdleStateKey) != idleState) {
                        EditorPrefs.SetString(DiscordRPC.RpcStateInfo.IdleStateKey, idleState);
                    }
                    GUILayout.EndHorizontal();
                    
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space(5);
                
                // Buttons for resetting and refreshing
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(120))) {
                    DiscordRPC.RpcStateInfo.ResetToDefaults();
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
    }
}
