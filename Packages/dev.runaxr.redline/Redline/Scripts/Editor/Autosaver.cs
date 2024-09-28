#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
  [InitializeOnLoad]
  public class Sceneautosave: EditorWindow {
    static Sceneautosave() {
      EditorApplication.update -= DoSplashScreen;
      EditorApplication.update += DoSplashScreen;
    }

    private static void DoSplashScreen() {
      EditorApplication.update -= DoSplashScreen;
      if (!EditorPrefs.HasKey("ShowSplashScreen")) {
        EditorPrefs.SetBool("ShowSplashScreen", true);
      }

      if (EditorPrefs.GetBool("ShowSplashScreen"))
        OpenSplashScreen();
    }

    private static GUIStyle _header;
    private static Vector2 _changeLogScroll;
    // NOTE: Pixy; made const int instead of const float because const float in C is stupid.
    private const int Timer = 60;
    private float _timeLeft;

    [MenuItem("Redline/Scene AutoSave", false, 500)]
    private static void Init()
    {
      var window=(Sceneautosave)GetWindow(typeof(Sceneautosave));
      window.Show();
    }

    private static void OpenSplashScreen() {
      GetWindowWithRect < Sceneautosave > (new Rect(0, 0, 400, 180), true);
    }

    public void OnEnable() {
      titleContent = new GUIContent("Auto Save");
      minSize = new Vector2(400, 200);
    }

    [Obsolete("Obsolete")]
    public void OnGUI() {
      EditorGUILayout.LabelField("Interval:", Timer + " seconds");

      var timeToSave = (int)(_timeLeft - EditorApplication.timeSinceStartup);

      EditorGUILayout.LabelField("Time to next save:", timeToSave + " seconds");

      Repaint();

      if (EditorApplication.timeSinceStartup > _timeLeft) {
        var path = EditorApplication.currentScene.Split(char.Parse("/"));
        path[ ^ 1] = "AutoSave_" + path[ ^ 1];
        EditorApplication.SaveScene(string.Join("/", path), true);
        _timeLeft = (int)(EditorApplication.timeSinceStartup + Timer);
      }

      GUILayout.BeginHorizontal();
      GUI.backgroundColor = Color.cyan;
      
      // TODO: remove these?
      if (GUILayout.Button("Trigon")) {
        Application.OpenURL("https://trigon.systems/");
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
        Application.OpenURL("https://status.trigon.systems");
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
      EditorPrefs.SetBool("ShowSplashScreen",
        GUILayout.Toggle(EditorPrefs.GetBool("ShowSplashScreen"), "Toggle AutoSave"));
      GUILayout.EndHorizontal();
    }
  }
}
#endif
