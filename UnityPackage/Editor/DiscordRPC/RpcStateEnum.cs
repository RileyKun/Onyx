using UnityEditor;

namespace Redline.Runtime.DiscordRPC {
    public static class RpcStateInfo {
        // Default state names
        private const string DefaultEditmodeState = "Modifying";
        private const string DefaultPlaymodeState = "Testing";
        private const string DefaultUploadpanelState = "Updating content";
        private const string DefaultIdleState = "Idle";
        private const int DefaultIdleTimerMinutes = 5; // Default idle timer: 5 minutes
        
        // EditorPrefs keys
        public const string EditmodeStateKey = "RedlineDiscordRPC_EditmodeState";
        public const string PlaymodeStateKey = "RedlineDiscordRPC_PlaymodeState";
        public const string UploadpanelStateKey = "RedlineDiscordRPC_UploadpanelState";
        public const string IdleStateKey = "RedlineDiscordRPC_IdleState";
        public const string IdleTimerKey = "RedlineDiscordRPC_IdleTimerMinutes";
        
        // Initialize default values if not set
        static RpcStateInfo() {
            if (!EditorPrefs.HasKey(EditmodeStateKey)) {
                EditorPrefs.SetString(EditmodeStateKey, DefaultEditmodeState);
            }
            
            if (!EditorPrefs.HasKey(PlaymodeStateKey)) {
                EditorPrefs.SetString(PlaymodeStateKey, DefaultPlaymodeState);
            }
            
            if (!EditorPrefs.HasKey(UploadpanelStateKey)) {
                EditorPrefs.SetString(UploadpanelStateKey, DefaultUploadpanelState);
            }
            
            if (!EditorPrefs.HasKey(IdleStateKey)) {
                EditorPrefs.SetString(IdleStateKey, DefaultIdleState);
            }
            
            if (!EditorPrefs.HasKey(IdleTimerKey)) {
                EditorPrefs.SetInt(IdleTimerKey, DefaultIdleTimerMinutes);
            }
        }
        
        public static string StateName(this RpcState state) {
            return state
                switch {
                    RpcState.Editmode => EditorPrefs.GetString(EditmodeStateKey, DefaultEditmodeState),
                    RpcState.Playmode => EditorPrefs.GetString(PlaymodeStateKey, DefaultPlaymodeState),
                    RpcState.Uploadpanel => EditorPrefs.GetString(UploadpanelStateKey, DefaultUploadpanelState),
                    RpcState.Idle => EditorPrefs.GetString(IdleStateKey, DefaultIdleState),
                    _ => EditorPrefs.GetString(IdleStateKey, DefaultIdleState)
                };
        }
        
        /// <summary>
        /// Gets the idle timer value in minutes
        /// </summary>
        public static int GetIdleTimerMinutes() {
            return EditorPrefs.GetInt(IdleTimerKey, DefaultIdleTimerMinutes);
        }
        
        // Helper methods to reset to defaults
        public static void ResetToDefaults() {
            EditorPrefs.SetString(EditmodeStateKey, DefaultEditmodeState);
            EditorPrefs.SetString(PlaymodeStateKey, DefaultPlaymodeState);
            EditorPrefs.SetString(UploadpanelStateKey, DefaultUploadpanelState);
            EditorPrefs.SetString(IdleStateKey, DefaultIdleState);
            EditorPrefs.SetInt(IdleTimerKey, DefaultIdleTimerMinutes);
        }
    }

    public enum RpcState {
        Editmode = 0,
        Playmode = 1,
        Uploadpanel = 2,
        Idle = 3
    }
}