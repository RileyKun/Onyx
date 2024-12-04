using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redline.DiscordRPC.Editor {
    [InitializeOnLoad]
    public static class RedlineDiscordRpcRuntimeHelper {
        // Register event handlers when the class is initialized
        static RedlineDiscordRpcRuntimeHelper() {
            EditorApplication.playModeStateChanged += LogPlayModeState;
            SceneManager.activeSceneChanged += SceneChanged;
            EditorApplication.quitting += OnEditorQuit; // Unsubscribe on quit
        }

        // Scene change handler
        private static void SceneChanged(Scene old, Scene next) {
            RedlineDiscordRPC.SceneChanged(next);
        }

        // Play mode state change handler
        private static void LogPlayModeState(PlayModeStateChange state) {
            switch (state) {
                case PlayModeStateChange.EnteredEditMode:
                    RedlineDiscordRPC.UpdateState(RpcState.Editmode);
                    RedlineDiscordRPC.ResetTime();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    RedlineDiscordRPC.UpdateState(RpcState.Playmode);
                    RedlineDiscordRPC.ResetTime();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    RedlineDiscordRPC.UpdateState(RpcState.ExitPlayMode); // Optionally update state on exit
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    RedlineDiscordRPC.UpdateState(RpcState.ExitEditMode); // Optionally update state on exit
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        // Unsubscribe event handlers when editor quits
        private static void OnEditorQuit() {
            EditorApplication.playModeStateChanged -= LogPlayModeState;
            SceneManager.activeSceneChanged -= SceneChanged;
        }
    }
}
