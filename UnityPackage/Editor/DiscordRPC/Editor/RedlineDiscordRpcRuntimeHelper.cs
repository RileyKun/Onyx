using System;
using UnityEditor;
using UnityEngine.SceneManagement;
using Redline.Runtime.DiscordRPC;

namespace Redline.Editor.DiscordRPC {
    [InitializeOnLoad]
    public static class RedlineDiscordRpcRuntimeHelper {
        // register an event handler when the class is initialized
        static RedlineDiscordRpcRuntimeHelper() {
            EditorApplication.playModeStateChanged += LogPlayModeState;
            SceneManager.activeSceneChanged += SceneChanged;
        }

        private static void SceneChanged(Scene old, Scene next) {
            RedlineDiscordRPC.SceneChanged(next);
        }

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
                case PlayModeStateChange.ExitingEditMode:
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}