using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redline.Scripts.Editor.DiscordRPC.Editor {
  [InitializeOnLoad]
  public class RedlineDiscordRPC {
    private static readonly DiscordRpc.RichPresence Presence = new();

    private static TimeSpan _time = (DateTime.UtcNow - new DateTime(1970, 1, 1));
    private static long _timestamp = (long) _time.TotalSeconds;

    private static RpcState _rpcState = RpcState.Editmode;
    private static readonly string GameName = Application.productName;
    private static string _sceneName = SceneManager.GetActiveScene().name;

    static RedlineDiscordRPC() {
      if (!EditorPrefs.GetBool("RedlineDiscordRPC", true)) return;
      RedlineLog("Starting DiscordRPC");
      var eventHandlers =
        default (DiscordRpc.EventHandlers);
      DiscordRpc.Initialize("1040718653202641006", ref eventHandlers, false, string.Empty);
      UpdateDrpc();
    }

    private static void UpdateDrpc() {
      RedlineLog("Updating everything");
      _sceneName = SceneManager.GetActiveScene().name;
      Presence.Details = $"Project: {GameName} Scene: {_sceneName}";
      Presence.State = "State: " + _rpcState.StateName();
      Presence.StartTimestamp = _timestamp;
      Presence.LargeImageKey = "rpm";
      Presence.LargeImageText = "Redline Package Manager";
      Presence.SmallImageKey = "axr";
      Presence.SmallImageText = "By AromaXR";
      DiscordRpc.UpdatePresence(Presence);
    }

    public static void UpdateState(RpcState state) {
      RedlineLog("Updating state to '" + state.StateName() + "'");
      _rpcState = state;
      Presence.State = "State: " + state.StateName();
      DiscordRpc.UpdatePresence(Presence);
    }

    public static void SceneChanged(Scene newScene) {
      RedlineLog("Updating scene name");
      _sceneName = newScene.name;
      Presence.Details = $"Project: {GameName} Scene: {_sceneName}";
      DiscordRpc.UpdatePresence(Presence);
    }

    public static void ResetTime() {
      RedlineLog("Resetting timer");
      _time = (DateTime.UtcNow - new DateTime(1970, 1, 1));
      _timestamp = (long) _time.TotalSeconds;
      Presence.StartTimestamp = _timestamp;

      DiscordRpc.UpdatePresence(Presence);
    }

    private static void RedlineLog(string message) {
      Debug.Log("[Redline] DiscordRPC: " + message);
    }
  }
}