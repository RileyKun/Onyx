using System;
using System.IO;
using UnityEditor;
using UnityEngine;

//using Amazon.S3.Model;

namespace dev.runaxr.redline.Redline.Scripts.Editor {
  [InitializeOnLoad]
  public class RedlineSettings: EditorWindow {
    private
    const string Url = "https://github.com/Redline/Redline/";
    private
    const string Url1 = "https://trigon.systems/";
    private
    const string Link = "";
    private
    const string Link1 = "";

    public
    const string ProjectConfigPath = "Packages/dev.runaxr.Redline/Redline/Configs/";
    private
    const string BackgroundConfig = "BackgroundVideo.txt";
    private
    const string ProjectDownloadPath = "Packages/dev.runaxr.Redline/Redline/Assets/";
    private static GUIStyle _toolkitHeader;
    public Color RedlineColor = Color.red;
    public static bool UITextRainbow {
      get;
      set;
    }
    //public Gradient RedlineGRADIENT;

    [MenuItem("Redline/Settings", false, 501)]
    private static void Init()
    {
      var window=(RedlineSettings)EditorWindow.GetWindow(typeof(RedlineSettings));
      window.Show();
    }

    public static string GetAssetPath() {
      if (EditorPrefs.GetBool("Redline_onlyProject", false)) {
        return ProjectDownloadPath;
      }

      var assetPath = EditorPrefs.GetString("Redline_customAssetPath", "%appdata%/Redline/")
        .Replace("%appdata%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
        .Replace("/", "\\");

      if (!assetPath.EndsWith("\\")) {
        assetPath += "\\";
      }

      Directory.CreateDirectory(assetPath);
      return assetPath;
    }

    public void OnEnable() {
      titleContent = new GUIContent("The Black Arms Settings");

      minSize = new Vector2(400, 600);

      _toolkitHeader = new GUIStyle {
        normal = {
            background = Resources.Load("RedlinePMHeader") as Texture2D,
            textColor = Color.white
          },
          fixedHeight = 200
      };

      if (!EditorPrefs.HasKey("RedlineDiscordRPC")) {
        EditorPrefs.SetBool("RedlineDiscordRPC", true);
      }

      if (File.Exists(ProjectConfigPath + BackgroundConfig) &&
        EditorPrefs.HasKey("Redline_background")) return;
      EditorPrefs.SetBool("Redline_background", false);
      File.WriteAllText(ProjectConfigPath + BackgroundConfig, "False");
    }

    public void OnGUI() {
      GUILayout.Box("", _toolkitHeader);
      GUILayout.Space(4);
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Redline")) {
        Application.OpenURL(Url);
      }

      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Trigon.Systems")) {
        Application.OpenURL(Url1);
      }

      GUILayout.EndHorizontal();
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );

      GUILayout.Space(4);
      EditorGUILayout.BeginVertical();
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );

      EditorGUILayout.LabelField("Redline Settings", EditorStyles.boldLabel);
      EditorGUILayout.Space(10);
      //if (GUILayout.Button("Set Color"))
      //{
      //    UnityEditor.EditorPrefs.SetFloat("RedlineColor_R", RedlineColor.r);
      //    UnityEditor.EditorPrefs.SetFloat("RedlineColor_G", RedlineColor.g);
      //    UnityEditor.EditorPrefs.SetFloat("RedlineColor_B", RedlineColor.b);
      //    UnityEditor.EditorPrefs.SetFloat("RedlineColor_A", RedlineColor.a);
      //}

      EditorGUI.BeginChangeCheck();

      RedlineColor = EditorGUI.ColorField(new Rect(3, 270, position.width - 6, 15), "Kit UI Color", RedlineColor);
      //RedlineGRADIENT = EditorGUI.GradientField(new Rect(3, 360, position.width - 6, 15), "Redline Gradient", RedlineGRADIENT);

      if (EditorGUI.EndChangeCheck()) {
        EditorPrefs.SetFloat("RedlineColor_R", RedlineColor.r);
        EditorPrefs.SetFloat("RedlineColor_G", RedlineColor.g);
        EditorPrefs.SetFloat("RedlineColor_B", RedlineColor.b);
        EditorPrefs.SetFloat("RedlineColor_A", RedlineColor.a);
      }

      EditorGUILayout.Space();
      if (GUILayout.Button("Reset Color")) {
        var RedlineColor = Color.red;

        EditorPrefs.SetFloat("RedlineColor_R", RedlineColor.r);
        EditorPrefs.SetFloat("RedlineColor_G", RedlineColor.g);
        EditorPrefs.SetFloat("RedlineColor_B", RedlineColor.b);
        EditorPrefs.SetFloat("RedlineColor_A", RedlineColor.a);
      }

      //RedlineGRADIENT = EditorGUI.GradientField(new Rect(3, 290, position.width - 6, 15), "Redline Gradient", RedlineGRADIENT);

      EditorGUILayout.Space(10);
      EditorGUILayout.EndVertical();
      GUILayout.Label("Overall:");
      GUILayout.BeginHorizontal();
      var isDiscordEnabled = EditorPrefs.GetBool("RedlineDiscordRPC", true);
      var enableDiscord = EditorGUILayout.ToggleLeft("Discord RPC", isDiscordEnabled);
      if (enableDiscord != isDiscordEnabled) {
        EditorPrefs.SetBool("RedlineDiscordRPC", enableDiscord);
      }

      GUILayout.EndHorizontal();
      //Hide Console logs
      GUILayout.Space(4);
      GUILayout.BeginHorizontal();
      var isHiddenConsole = EditorPrefs.GetBool("Redline_HideConsole");
      var enableConsoleHide = EditorGUILayout.ToggleLeft("Hide Console Errors", isHiddenConsole);
      switch (enableConsoleHide)
      {
        case true:
          EditorPrefs.SetBool("Redline_HideConsole", true);
          Debug.ClearDeveloperConsole();
          Debug.unityLogger.logEnabled = false;
          break;
        case false:
          EditorPrefs.SetBool("Redline_HideConsole", false);
          Debug.ClearDeveloperConsole();
          Debug.unityLogger.logEnabled = true;
          break;
      }

      GUILayout.EndHorizontal();
      GUILayout.Space(4);
      GUILayout.BeginHorizontal();
      var isUITextRainbowEnabled = EditorPrefs.GetBool("Redline_UITextRainbow", false);
      var enableUITextRainbow = EditorGUILayout.ToggleLeft("Rainbow Text", isUITextRainbowEnabled);
      if (enableUITextRainbow != isUITextRainbowEnabled) {
        EditorPrefs.SetBool("Redline_UITextRainbow", enableUITextRainbow);
        UITextRainbow = true;
      } else {
        UITextRainbow = false;
      }

      GUILayout.EndHorizontal();
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
      GUI.backgroundColor = new Color(
        EditorPrefs.GetFloat("RedlineColor_R"),
        EditorPrefs.GetFloat("RedlineColor_G"),
        EditorPrefs.GetFloat("RedlineColor_B"),
        EditorPrefs.GetFloat("RedlineColor_A")
      );
      GUILayout.Label("Asset path:");
      GUILayout.BeginHorizontal();
      var customAssetPath = EditorGUILayout.TextField("",
        EditorPrefs.GetString("Redline_customAssetPath", "%appdata%/Redline/"));
      if (GUILayout.Button("Choose", GUILayout.Width(60))) {
        var path = EditorUtility.OpenFolderPanel("Asset download folder",
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Redline");
        if (path != "") {
          Debug.Log(path);
          customAssetPath = path;
        }
      }

      if (GUILayout.Button("Reset", GUILayout.Width(50))) {
        customAssetPath = "%appdata%/Redline/";
      }

      if (EditorPrefs.GetString("Redline_customAssetPath", "%appdata%/Redline/") != customAssetPath) {
        EditorPrefs.SetString("Redline_customAssetPath", customAssetPath);
      }

      GUILayout.EndHorizontal();
    }
  }
}
// Soph waz 'ere