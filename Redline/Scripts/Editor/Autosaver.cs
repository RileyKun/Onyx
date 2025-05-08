#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
  [InitializeOnLoad]
  public class Sceneautosave: EditorWindow {
    private const string AutosaveEnabledKey = "Redline_Autosave_Enabled";
    private const string ShowSplashScreenKey = "ShowSplashScreen";
    private const string AutosaveIntervalKey = "Redline_Autosave_Interval";
    private const int DefaultInterval = 300; // 5 minutes default
    private static bool _hasCheckedInitialSetup;

    static Sceneautosave() {
      EditorApplication.update -= DoSplashScreen;
      EditorApplication.update += DoSplashScreen;
    }

    private static int AutoSaveInterval {
      get => EditorPrefs.GetInt(AutosaveIntervalKey, DefaultInterval);
      set {
        if (value < 30) value = 30; // Minimum 30 seconds
        if (value > 3600) value = 3600; // Maximum 1 hour
        EditorPrefs.SetInt(AutosaveIntervalKey, value);
      }
    }

    private static void DoSplashScreen() {
      EditorApplication.update -= DoSplashScreen;
      if (!EditorPrefs.HasKey(ShowSplashScreenKey)) {
        EditorPrefs.SetBool(ShowSplashScreenKey, true);
      }

      if (EditorPrefs.GetBool(ShowSplashScreenKey))
        OpenSplashScreen();
    }

    private static GUIStyle _header;
    private static Vector2 _changeLogScroll;
    private float _timeLeft;
    private static bool _mBHasShownPrompt;

    [MenuItem("Redline/Scene AutoSave", false, 500)]
    private static void Init() {
      var window = (Sceneautosave)GetWindow(typeof(Sceneautosave));
      if (_mBHasShownPrompt) return;
      window.Show();
      _mBHasShownPrompt = true;
    }

    private static void OpenSplashScreen() {
  var window = GetWindow<Sceneautosave>(true);
  window.position = new Rect(0, 0, 400, 180);
}

    public void OnEnable() {
      titleContent = new GUIContent("Auto Save");
      minSize = new Vector2(400, 200);

      switch (_hasCheckedInitialSetup)
      {
	      case false when !EditorPrefs.HasKey(AutosaveEnabledKey):
	      {
		      EditorPrefs.SetBool(AutosaveEnabledKey, EditorUtility.DisplayDialog(
			      "Redline Autosave",
			      "Would you like to enable automatic scene saving? This will save your scene every " +
			      (AutoSaveInterval / 60) + " minutes.",
			      "Enable", "Disable"));
		      _hasCheckedInitialSetup = true;
		      break;
	      }
      }
    }

    [Obsolete("Obsolete")]
    public void OnGUI() {
      // Autosave interval slider
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Autosave Interval (seconds):");
      var newInterval = EditorGUILayout.IntSlider(AutoSaveInterval, 30, 3600);
      if (newInterval != AutoSaveInterval) {
        AutoSaveInterval = newInterval;
        _timeLeft = (float)(EditorApplication.timeSinceStartup + AutoSaveInterval);
      }

      // Time to next save
      var timeToSave = (int)(_timeLeft - EditorApplication.timeSinceStartup);
      EditorGUILayout.LabelField("Time to next save:", timeToSave + " seconds");
      EditorGUILayout.Space(10);

      Repaint();

      if (EditorApplication.timeSinceStartup > _timeLeft && EditorPrefs.GetBool(AutosaveEnabledKey)) {
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
      EditorPrefs.SetBool(AutosaveEnabledKey,
        GUILayout.Toggle(EditorPrefs.GetBool(AutosaveEnabledKey), "Enable AutoSave"));
      GUILayout.EndHorizontal();
    }
  }
}
#endif
