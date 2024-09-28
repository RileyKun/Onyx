using System.Collections.Generic;
using System.IO;
using RedlineUpdater.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
  public class RedlinePackageManager: EditorWindow {
    private
    const string Url = "https://github.com/Redline/Redline/";
    private
    const string Url1 = "https://trigon.systems/";

    private static GUIStyle _redlineHeader;
    private static readonly Dictionary < string, string > Assets = new();
    private static Vector2 _changeLogScroll;

    [MenuItem("Redline/Package Manager", false, 501)]
    private static void Init()
    {
      var window=(RedlinePackageManager)GetWindow(typeof(RedlinePackageManager));
      window.Show();
    }

    public void OnEnable() {
      titleContent = new GUIContent("Redline Package Manager");

      minSize = new Vector2(400, 600);
      RedlineImportManager.CheckForConfigUpdate();
      LoadJson();

      _redlineHeader = new GUIStyle {
        normal = {
            background = Resources.Load("RedlinePMHeader") as Texture2D,
            textColor = Color.white
          },
          fixedHeight = 200
      };
    }

    public static void LoadJson() {
      Assets.Clear();

      dynamic configJson =
        JObject.Parse(File.ReadAllText(RedlineSettings.ProjectConfigPath +
          RedlineImportManager.ConfigName));

      Debug.Log("Server Asset Url is: " + configJson["config"]["serverUrl"]);
      RedlineImportManager.ServerUrl = configJson["config"]["serverUrl"].ToString();

      foreach(JProperty x in configJson["assets"]) {
        var value = x.Value;

        var buttonName = "";
        var file = "";

        foreach(var jToken in value) {
          var y = (JProperty) jToken;
          switch (y.Name) {
          case "name":
            buttonName = y.Value.ToString();
            break;
          case "file":
            file = y.Value.ToString();
            break;
          }
        }

        Assets[buttonName] = file;
      }
    }

    public void OnGUI() {
      GUILayout.Box("", style: _redlineHeader);
      GUILayout.Space(4);
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Check for Updates")) {
        RedlineAutomaticUpdateAndInstall.AutomaticRedlineInstaller();
      }

      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Redline")) {
        Application.OpenURL(Url);
      }

      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Trigon.Systems")) {
        Application.OpenURL(Url1);
      }

      GUILayout.EndHorizontal();
      GUILayout.Space(4);
      //Update Assets Config
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Update Config")) {
        RedlineImportManager.UpdateConfig();
      }

      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      GUILayout.EndHorizontal();
      GUILayout.Space(4);

      //Imports V!V
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );
      _changeLogScroll = GUILayout.BeginScrollView(_changeLogScroll, GUILayout.Width(0));
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );
      foreach(var asset in Assets) {
        GUILayout.BeginHorizontal();
        if (asset.Value == "") {
          GUILayout.FlexibleSpace();
          GUILayout.Label(asset.Key);
          GUILayout.FlexibleSpace();
        } else {
          if (GUILayout.Button(
              (File.Exists(RedlineSettings.GetAssetPath() + asset.Value) ? "Import" : "Download") +
              " " + asset.Key)) {
            RedlineImportManager.DownloadAndImportAssetFromServer(asset.Value);
          }

          if (GUILayout.Button("Del", GUILayout.Width(40))) {
            RedlineImportManager.DeleteAsset(asset.Value);
          }
        }

        GUILayout.EndHorizontal();
      }

      GUILayout.EndScrollView();
    }
  }
}