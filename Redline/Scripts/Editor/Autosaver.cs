#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redline.Scripts.Editor {
  /// <summary>
  /// Automatically saves the current scene at configurable intervals
  /// </summary>
  [InitializeOnLoad]
  public class Sceneautosave : EditorWindow {
    // Preference keys
    private const string AUTOSAVE_ENABLED_KEY = "Redline_Autosave_Enabled";
    private const string SHOW_SPLASH_SCREEN_KEY = "ShowSplashScreen";
    private const string AUTOSAVE_INTERVAL_KEY = "Redline_Autosave_Interval";
    
    // Default values
    private const int DEFAULT_INTERVAL = 300; // 5 minutes default
    private const int MIN_INTERVAL = 30;      // Minimum 30 seconds
    private const int MAX_INTERVAL = 3600;    // Maximum 1 hour (3600 seconds)
    
    // UI elements
    private static Vector2 _scrollPosition;
    
    // Timer variables
    private float _timeLeft;
    private float _lastTimeSinceStartup;
    
    // State tracking
    private static bool _hasCheckedInitialSetup;
    private static bool _hasShownPrompt;

    /// <summary>
    /// Static constructor called when Unity Editor starts
    /// </summary>
    static Sceneautosave() {
      EditorApplication.update -= DoSplashScreen;
      EditorApplication.update += DoSplashScreen;
    }

    /// <summary>
    /// Gets or sets the autosave interval with validation
    /// </summary>
    private static int AutoSaveInterval {
      get => EditorPrefs.GetInt(AUTOSAVE_INTERVAL_KEY, DEFAULT_INTERVAL);
      set {
        int validValue = Mathf.Clamp(value, MIN_INTERVAL, MAX_INTERVAL);
        EditorPrefs.SetInt(AUTOSAVE_INTERVAL_KEY, validValue);
      }
    }

    /// <summary>
    /// Shows the splash screen on first load
    /// </summary>
    private static void DoSplashScreen() {
      EditorApplication.update -= DoSplashScreen;
      
      if (!EditorPrefs.HasKey(SHOW_SPLASH_SCREEN_KEY)) {
        EditorPrefs.SetBool(SHOW_SPLASH_SCREEN_KEY, true);
      }

      if (EditorPrefs.GetBool(SHOW_SPLASH_SCREEN_KEY)) {
        OpenSplashScreen();
      }
    }

    /// <summary>
    /// Menu item to open the autosave window
    /// </summary>
    [MenuItem("Redline/Scene AutoSave", false, 500)]
    private static void Init() {
      var window = (Sceneautosave)GetWindow(typeof(Sceneautosave));
      if (_hasShownPrompt) return;
      window.Show();
      _hasShownPrompt = true;
    }

    /// <summary>
    /// Opens the splash screen
    /// </summary>
    private static void OpenSplashScreen() {
      var window = GetWindow<Sceneautosave>(true);
      window.position = new Rect(0, 0, 400, 180);
    }

    /// <summary>
    /// Called when the window is enabled
    /// </summary>
    public void OnEnable() {
      titleContent = new GUIContent("Auto Save");
      minSize = new Vector2(400, 200);
      
      // Initialize timer when window opens
      _timeLeft = AutoSaveInterval;
      _lastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;

      // Check if we need to show the initial setup dialog
      if (!_hasCheckedInitialSetup && !EditorPrefs.HasKey(AUTOSAVE_ENABLED_KEY)) {
        int intervalMinutes = AutoSaveInterval / 60;
        string message = $"Would you like to enable automatic scene saving? This will save your scene every {intervalMinutes} minutes.";
        
        EditorPrefs.SetBool(AUTOSAVE_ENABLED_KEY, EditorUtility.DisplayDialog(
          "Redline Autosave",
          message,
          "Enable", "Disable"));
          
        _hasCheckedInitialSetup = true;
      }
    }

    /// <summary>
    /// Draws the GUI
    /// </summary>
    public void OnGUI() {
      DrawIntervalSettings();
      UpdateTimer();
      DrawTimerDisplay();
      DrawButtons();
      DrawToggle();
      
      // Check if it's time to save
      if (_timeLeft <= 0 && IsAutosaveEnabled()) {
        SaveScene();
        _timeLeft = AutoSaveInterval;
      }
      
      // Request repaint to update timer
      Repaint();
    }
    
    /// <summary>
    /// Draws the interval settings UI
    /// </summary>
    private void DrawIntervalSettings() {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Autosave Interval (seconds):");
      
      int newInterval = EditorGUILayout.IntSlider(AutoSaveInterval, MIN_INTERVAL, MAX_INTERVAL);
      if (newInterval != AutoSaveInterval) {
        AutoSaveInterval = newInterval;
        _timeLeft = AutoSaveInterval; // Reset timer when interval changes
      }
    }
    
    /// <summary>
    /// Updates the timer countdown
    /// </summary>
    private void UpdateTimer() {
      if (IsAutosaveEnabled()) {
        float deltaTime = (float)EditorApplication.timeSinceStartup - _lastTimeSinceStartup;
        _timeLeft -= deltaTime;
      }
      _lastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;
    }
    
    /// <summary>
    /// Draws the timer display in MM:SS format
    /// </summary>
    private void DrawTimerDisplay() {
      // Ensure time is within valid range
      int timeToSave = Mathf.Clamp((int)_timeLeft, 0, AutoSaveInterval);
      
      // Format as MM:SS
      int minutes = timeToSave / 60;
      int seconds = timeToSave % 60;
      string timeDisplay = $"{minutes:00}:{seconds:00}";
      
      EditorGUILayout.LabelField("Time to next save:", timeDisplay);
      EditorGUILayout.Space(10);
    }
    
    /// <summary>
    /// Draws the link buttons
    /// </summary>
    private void DrawButtons() {
      // First row of buttons
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
      
      // Second row with status button
      GUILayout.BeginHorizontal();
      
      GUI.backgroundColor = Color.gray;
      if (GUILayout.Button("Status")) {
        Application.OpenURL("https://status.arch-linux.pro");
      }
      
      GUI.backgroundColor = Color.white;
      GUILayout.EndHorizontal();
      
      // Empty row for spacing
      GUILayout.BeginHorizontal();
      GUILayout.EndHorizontal();
      
      // Scrollview (kept for compatibility)
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
      GUILayout.EndScrollView();
      
      GUILayout.FlexibleSpace();
    }
    
    /// <summary>
    /// Draws the enable/disable toggle
    /// </summary>
    private void DrawToggle() {
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      
      bool isEnabled = IsAutosaveEnabled();
      bool newValue = GUILayout.Toggle(isEnabled, "Enable AutoSave");
      
      if (newValue != isEnabled) {
        EditorPrefs.SetBool(AUTOSAVE_ENABLED_KEY, newValue);
      }
      
      GUILayout.EndHorizontal();
    }
    
    /// <summary>
    /// Saves the current scene with an AutoSave prefix
    /// </summary>
    private void SaveScene() {
      Scene currentScene = SceneManager.GetActiveScene();
      string scenePath = currentScene.path;
      
      if (string.IsNullOrEmpty(scenePath)) {
        Debug.LogWarning("Cannot autosave a scene that hasn't been saved yet.");
        return;
      }
      
      string directory = Path.GetDirectoryName(scenePath);
      string fileName = Path.GetFileName(scenePath);
      string autoSavePath = Path.Combine(directory, "AutoSave_" + fileName);
      
      EditorSceneManager.SaveScene(currentScene, autoSavePath, true);
    }
    
    /// <summary>
    /// Checks if autosave is enabled
    /// </summary>
    private bool IsAutosaveEnabled() {
      return EditorPrefs.GetBool(AUTOSAVE_ENABLED_KEY, false);
    }
  }
}
#endif
