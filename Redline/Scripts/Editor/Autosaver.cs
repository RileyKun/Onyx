#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
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
    private const string AUTOSAVE_PREFIX_KEY = "Redline_Autosave_Prefix";
    private const string MAX_AUTOSAVES_KEY = "Redline_Max_Autosaves";
    private const string EXCLUDED_SCENES_KEY = "Redline_Excluded_Scenes";
    
    // Default values
    private const int DEFAULT_INTERVAL = 300; // 5 minutes default
    private const int MIN_INTERVAL = 30;      // Minimum 30 seconds
    private const int MAX_INTERVAL = 3600;    // Maximum 1 hour (3600 seconds)
    private const string DEFAULT_PREFIX = "AutoSave_";
    private const int DEFAULT_MAX_AUTOSAVES = 5;
    
    // UI elements
    private static Vector2 _scrollPosition;
    private string _excludedScenesText = "";
    
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
    /// Gets or sets the autosave prefix
    /// </summary>
    private static string AutoSavePrefix {
      get => EditorPrefs.GetString(AUTOSAVE_PREFIX_KEY, DEFAULT_PREFIX);
      set => EditorPrefs.SetString(AUTOSAVE_PREFIX_KEY, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of autosaves to keep
    /// </summary>
    private static int MaxAutoSaves {
      get => EditorPrefs.GetInt(MAX_AUTOSAVES_KEY, DEFAULT_MAX_AUTOSAVES);
      set => EditorPrefs.SetInt(MAX_AUTOSAVES_KEY, Mathf.Max(1, value));
    }

    /// <summary>
    /// Gets or sets the excluded scenes
    /// </summary>
    private static string[] ExcludedScenes {
      get => EditorPrefs.GetString(EXCLUDED_SCENES_KEY, "").Split(',', StringSplitOptions.RemoveEmptyEntries);
      set => EditorPrefs.SetString(EXCLUDED_SCENES_KEY, string.Join(",", value));
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
      DrawAdvancedSettings();
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
    /// Draws the advanced settings UI
    /// </summary>
    private void DrawAdvancedSettings() {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
      
      // Autosave prefix
      string newPrefix = EditorGUILayout.TextField("Autosave Prefix", AutoSavePrefix);
      if (newPrefix != AutoSavePrefix) {
        AutoSavePrefix = newPrefix;
      }
      
      // Max autosaves
      int newMaxSaves = EditorGUILayout.IntField("Max Autosaves", MaxAutoSaves);
      if (newMaxSaves != MaxAutoSaves) {
        MaxAutoSaves = newMaxSaves;
      }
      
      // Excluded scenes
      EditorGUILayout.LabelField("Excluded Scenes (comma-separated):");
      _excludedScenesText = EditorGUILayout.TextField(_excludedScenesText);
      if (GUILayout.Button("Update Excluded Scenes")) {
        ExcludedScenes = _excludedScenesText.Split(',', StringSplitOptions.RemoveEmptyEntries)
          .Select(s => s.Trim())
          .ToArray();
      }
      
      EditorGUILayout.Space();
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

      // Check if scene is excluded
      if (ExcludedScenes.Contains(Path.GetFileNameWithoutExtension(scenePath))) {
        Debug.Log($"Scene {scenePath} is excluded from autosave.");
        return;
      }
      
      string directory = Path.GetDirectoryName(scenePath);
      string fileName = Path.GetFileName(scenePath);
      string autoSavePath = Path.Combine(directory, AutoSavePrefix + fileName);
      
      try {
        EditorSceneManager.SaveScene(currentScene, autoSavePath, true);
        Debug.Log($"Scene autosaved to: {autoSavePath}");
        
        // Do not use dialogs again Aroma, this is all we need
        Debug.Log($"[Redline Autosave] Scene has been automatically saved to: {autoSavePath}");
        
        // Clean up old autosaves
        CleanupOldAutosaves(directory, fileName);
      }
      catch (Exception e) {
        Debug.LogError($"Failed to autosave scene: {e.Message}");
      }
    }
    
    /// <summary>
    /// Cleans up old autosave files
    /// </summary>
    private void CleanupOldAutosaves(string directory, string originalFileName) {
      try {
        string pattern = $"{AutoSavePrefix}{originalFileName}";
        var autosaves = Directory.GetFiles(directory, pattern)
          .OrderByDescending(f => File.GetLastWriteTime(f))
          .ToList();

        // Remove excess autosaves
        while (autosaves.Count > MaxAutoSaves) {
          string oldestFile = autosaves.Last();
          File.Delete(oldestFile);
          autosaves.RemoveAt(autosaves.Count - 1);
          Debug.Log($"Removed old autosave: {oldestFile}");
        }
      }
      catch (Exception e) {
        Debug.LogError($"Failed to cleanup old autosaves: {e.Message}");
      }
    }
    
    /// <summary>
    /// Checks if autosave is enabled
    /// </summary>
    private bool IsAutosaveEnabled() {
      return EditorPrefs.GetBool(AUTOSAVE_ENABLED_KEY, false);
    }

    /// <summary>
    /// Shows a menu to restore from autosaves
    /// </summary>
    [MenuItem("Redline/Restore from Autosave")]
    private static void ShowRestoreMenu() {
      Scene currentScene = SceneManager.GetActiveScene();
      string scenePath = currentScene.path;
      
      if (string.IsNullOrEmpty(scenePath)) {
        EditorUtility.DisplayDialog("Restore Failed", 
          "Cannot restore from autosave for an unsaved scene.", 
          "OK");
        return;
      }

      string directory = Path.GetDirectoryName(scenePath);
      string fileName = Path.GetFileName(scenePath);
      string pattern = $"{AutoSavePrefix}{fileName}";
      
      var autosaves = Directory.GetFiles(directory, pattern)
        .OrderByDescending(f => File.GetLastWriteTime(f))
        .ToList();

      if (autosaves.Count == 0) {
        EditorUtility.DisplayDialog("No Autosaves", 
          "No autosaves found for the current scene.", 
          "OK");
        return;
      }

      // Create menu items for each autosave
      GenericMenu menu = new GenericMenu();
      foreach (var autosave in autosaves) {
        string timestamp = File.GetLastWriteTime(autosave).ToString("yyyy-MM-dd HH:mm:ss");
        menu.AddItem(new GUIContent($"Restore from {timestamp}"), false, () => RestoreFromAutosave(autosave));
      }
      
      menu.ShowAsContext();
    }

    /// <summary>
    /// Restores the scene from an autosave file
    /// </summary>
    private static void RestoreFromAutosave(string autosavePath) {
      if (EditorUtility.DisplayDialog("Restore from Autosave",
          "Are you sure you want to restore from this autosave? Current changes will be lost.",
          "Restore", "Cancel")) {
        try {
          EditorSceneManager.OpenScene(autosavePath);
          Debug.Log($"Restored scene from: {autosavePath}");
        }
        catch (Exception e) {
          Debug.LogError($"Failed to restore from autosave: {e.Message}");
        }
      }
    }
  }
}
#endif
