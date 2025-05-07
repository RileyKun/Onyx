#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
  [InitializeOnLoad]
  public class Sceneautosave: EditorWindow {
    private const string AUTOSAVE_ENABLED_KEY = "Redline_Autosave_Enabled";
    private const string SHOW_SPLASH_SCREEN_KEY = "ShowSplashScreen";
    private const string AUTOSAVE_INTERVAL_KEY = "Redline_Autosave_Interval";
    private const int DEFAULT_INTERVAL = 300; // 5 minutes default
    private static bool _hasCheckedInitialSetup = false;
    
    static Sceneautosave() {
      EditorApplication.update -= DoSplashScreen;
      EditorApplication.update += DoSplashScreen;
    }

    private static int AutoSaveInterval {
      get { return EditorPrefs.GetInt(AUTOSAVE_INTERVAL_KEY, DEFAULT_INTERVAL); }
      set { 
        if (value < 30) value = 30; // Minimum 30 seconds
        if (value > 3600) value = 3600; // Maximum 1 hour
        EditorPrefs.SetInt(AUTOSAVE_INTERVAL_KEY, value); 
      }
    }

    private static void DoSplashScreen() {
      EditorApplication.update -= DoSplashScreen;
      if (!EditorPrefs.HasKey(SHOW_SPLASH_SCREEN_KEY)) {
        EditorPrefs.SetBool(SHOW_SPLASH_SCREEN_KEY, true);
      }

      if (EditorPrefs.GetBool(SHOW_SPLASH_SCREEN_KEY))
        OpenSplashScreen();
    }

    private static GUIStyle _header;
    private static Vector2 _changeLogScroll;
    private float _timeLeft;
    private static bool m_bHasShownPrompt = false;

    [MenuItem("Redline/Scene AutoSave", false, 500)]
    private static void Init() {
      var window = (Sceneautosave)GetWindow(typeof(Sceneautosave));
      if (!m_bHasShownPrompt) {
        window.Show();
        m_bHasShownPrompt = true;
      }
    }

    private static void OpenSplashScreen() {
  var window = GetWindow<Sceneautosave>(true);
  window.position = new Rect(0, 0, 400, 180);
}

    public void OnEnable() {
      titleContent = new GUIContent("Auto Save");
      minSize = new Vector2(400, 200);

      if (!_hasCheckedInitialSetup && !EditorPrefs.HasKey(AUTOSAVE_ENABLED_KEY)) {
        if (EditorUtility.DisplayDialog(
          "Redline Autosave",
          "Would you like to enable automatic scene saving? This will save your scene every " + (AutoSaveInterval / 60) + " minutes.",
          "Enable", "Disable")) {
          EditorPrefs.SetBool(AUTOSAVE_ENABLED_KEY, true);
        } else {
          EditorPrefs.SetBool(AUTOSAVE_ENABLED_KEY, false);
        }
        _hasCheckedInitialSetup = true;
      }
    }

    [Obsolete("Obsolete")]
    public void OnGUI() {
      // Autosave interval slider
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Autosave Interval (seconds):");
      int newInterval = EditorGUILayout.IntSlider(AutoSaveInterval, 30, 3600);
      if (newInterval != AutoSaveInterval) {
        AutoSaveInterval = newInterval;
        _timeLeft = (float)(EditorApplication.timeSinceStartup + AutoSaveInterval);
      }

      // Time to next save
      var timeToSave = (int)(_timeLeft - EditorApplication.timeSinceStartup);
      EditorGUILayout.LabelField("Time to next save:", timeToSave + " seconds");
      EditorGUILayout.Space(10);

      Repaint();

      if (EditorApplication.timeSinceStartup > _timeLeft && EditorPrefs.GetBool(AUTOSAVE_ENABLED_KEY)) {
        var path = EditorApplication.currentScene.Split(char.Parse("/"));
        path[^1] = "AutoSave_" + path[^1];
        EditorApplication.SaveScene(string.Join("/", path), true);
        _timeLeft = (float)(EditorApplication.timeSinceStartup + AutoSaveInterval);
      }

      // Buttons
      GUILayout.BeginHorizontal();
      GUI.backgroundColor = Color.cyan;
      
      if (GUILayout.Button("AL.Pro")) {
        Application.OpenURL("https://arch-linux.pro/");
      }

      GUI.backgroundColor = Color.red;
      if (GUILayout.Button("Github")) {
        Application.OpenURL("https://github.com/Redline-Team/RPM");
      }

      GUI.backgroundColor = Color.yellow;
      if (GUILayout.Button("C0deRa1n")) {
        Application.OpenURL("https://c0dera.in/");
      }

      GUI.backgroundColor = Color.green;
      if (GUILayout.Button("Support")) {
        Application.OpenURL("https://github.com/Redline-Team/RPM/issues");
      }

      GUI.backgroundColor = Color.white;
      GUILayout.EndHorizontal();
      GUILayout.Space(0);

      GUILayout.BeginHorizontal();
      GUI.backgroundColor = Color.gray;

      if (GUILayout.Button("Status")) {
        Application.OpenURL("https://status.arch-linux.pro");
      }

      GUI.backgroundColor = Color.white;
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();

      GUI.backgroundColor = Color.white;
      GUILayout.EndHorizontal();
      GUILayout.Space(0);

      _changeLogScroll = GUILayout.BeginScrollView(_changeLogScroll);
      GUILayout.EndScrollView();

      GUILayout.FlexibleSpace();

      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      EditorPrefs.SetBool(AUTOSAVE_ENABLED_KEY,
        GUILayout.Toggle(EditorPrefs.GetBool(AUTOSAVE_ENABLED_KEY), "Enable AutoSave"));
      GUILayout.EndHorizontal();
    }
  }
}
#endif