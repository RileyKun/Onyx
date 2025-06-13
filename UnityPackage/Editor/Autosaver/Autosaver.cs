#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redline.Editor.Autosaver {
  /// <summary>
  /// Automatically saves the current scene at configurable intervals with enhanced features
  /// </summary>
  [InitializeOnLoad]
  public class Sceneautosave : EditorWindow {
    /// <summary>
    /// Save information
    /// </summary>
    private class SaveInfo {
      public string FilePath { get; set; }
      public DateTime SaveTime { get; set; }
      public long FileSize { get; set; }
      public bool HasMetadata { get; set; }
    }

    /// <summary>
    /// Scene-specific settings
    /// </summary>
    private class SceneSettings {
      public bool Enabled { get; set; } = true;
      public int CustomInterval { get; set; } = -1; // -1 means use global interval
      public bool SaveOnPlay { get; set; } = true;
      public bool SaveOnCompile { get; set; } = true;
      public string[] ExcludedAssets { get; set; } = new string[0];
      public string[] RequiredAssets { get; set; } = new string[0];
    }

    /// <summary>
    /// Custom save trigger
    /// </summary>
    private class CustomTrigger {
      public string Name { get; set; }
      public TriggerType Type { get; set; }
      public string Condition { get; set; }
      public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Trigger types
    /// </summary>
    private enum TriggerType {
      TimeBased,
      EventBased,
      ConditionBased
    }

    /// <summary>
    /// Backup strategies
    /// </summary>
    private enum BackupStrategy {
      Rotating,    // Keep N most recent backups
      Incremental, // Keep all backups with incremental naming
      Timestamped, // Keep backups with timestamps
      Hybrid       // Combination of strategies
    }

    // Preference keys
    private const string AUTOSAVE_ENABLED_KEY = "Redline_Autosave_Enabled";
    private const string AUTOSAVE_INTERVAL_KEY = "Redline_Autosave_Interval";
    private const string AUTOSAVE_PREFIX_KEY = "Redline_Autosave_Prefix";
    private const string MAX_AUTOSAVES_KEY = "Redline_Max_Autosaves";
    private const string EXCLUDED_SCENES_KEY = "Redline_Excluded_Scenes";
    private const string SAVE_ON_PLAY_KEY = "Redline_Save_On_Play";
    private const string SAVE_ON_COMPILE_KEY = "Redline_Save_On_Compile";
    private const string BACKUP_LOCATIONS_KEY = "Redline_Backup_Locations";
    private const string COMPRESS_SAVES_KEY = "Redline_Compress_Saves";
    private const string SAVE_METADATA_KEY = "Redline_Save_Metadata";
    private const string SAVE_ALL_SCENES_KEY = "Redline_Save_All_Scenes";
    private const string SAVE_DEPENDENCIES_KEY = "Redline_Save_Dependencies";
    private const string SCENE_GROUPS_KEY = "Redline_Scene_Groups";
    private const string SHOW_NOTIFICATIONS_KEY = "Redline_Autosave_ShowNotifications";
    private const string NOTIFICATION_DURATION_KEY = "Redline_Autosave_NotificationDuration";
    private const string SHOW_TIMELINE_KEY = "Redline_Autosave_ShowTimeline";
    private const string SCENE_SETTINGS_KEY = "Redline_Autosave_SceneSettings";
    private const string CUSTOM_TRIGGERS_KEY = "Redline_Autosave_CustomTriggers";
    private const string BACKUP_STRATEGY_KEY = "Redline_Autosave_BackupStrategy";
    private const string VALIDATION_ENABLED_KEY = "Redline_Autosave_ValidationEnabled";
    
    // Default values
    private const int DEFAULT_INTERVAL = 300; // 5 minutes default
    private const int MIN_INTERVAL = 30;      // Minimum 30 seconds
    private const int MAX_INTERVAL = 3600;    // Maximum 1 hour (3600 seconds)
    private const string DEFAULT_PREFIX = "AutoSave_";
    private const int DEFAULT_MAX_AUTOSAVES = 5;
    private const bool DEFAULT_COMPRESS_SAVES = true;
    private const bool DEFAULT_SAVE_ALL_SCENES = true;
    private const bool DEFAULT_SAVE_DEPENDENCIES = true;
    private const bool DEFAULT_SHOW_NOTIFICATIONS = true;
    private const float DEFAULT_NOTIFICATION_DURATION = 3f;
    private const bool DEFAULT_SHOW_TIMELINE = true;
    private const bool DEFAULT_VALIDATION_ENABLED = true;
    
    // UI elements
    private static Vector2 _scrollPosition;
    private string _excludedScenesText = "";
    private Dictionary<string, DateTime> _lastSaveTimes = new Dictionary<string, DateTime>();
    private List<string> _backupLocations = new List<string>();
    private bool _compressSaves = true;
    private bool _saveMetadata = true;
    private bool _saveAllScenes = true;
    private Dictionary<string, List<string>> _sceneGroups = new Dictionary<string, List<string>>();
    private bool _showSceneGroups = false;
    private bool _showNotifications = true;
    private float _notificationDuration = 3f;
    private bool _showTimeline = true;
    private Vector2 _timelineScrollPosition;
    private Dictionary<string, List<SaveInfo>> _saveHistory = new Dictionary<string, List<SaveInfo>>();
    private string _selectedScene = "";
    private SaveInfo _selectedSave;
    
    // Timer variables
    private double _nextSaveTime;
    private static bool _isPlayMode = false;
    private static bool _wasEnabled = false;  // Track previous enabled state
    
    // New UI elements
    private Dictionary<string, SceneSettings> _sceneSettings = new Dictionary<string, SceneSettings>();
    private List<CustomTrigger> _customTriggers = new List<CustomTrigger>();
    private BackupStrategy _backupStrategy = BackupStrategy.Rotating;
    private bool _validationEnabled = true;
    private bool _showSceneSettings = false;
    private bool _showCustomTriggers = false;
    private bool _showBackupSettings = false;
    private bool _showValidationSettings = false;

    /// <summary>
    /// Static constructor called when Unity Editor starts
    /// </summary>
    static Sceneautosave() {
      EditorApplication.update -= OnEditorUpdate;
      EditorApplication.update += OnEditorUpdate;
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
      AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
      AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
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
    /// Gets or sets whether to save when entering play mode
    /// </summary>
    private static bool SaveOnPlay {
      get => EditorPrefs.GetBool(SAVE_ON_PLAY_KEY, true);
      set => EditorPrefs.SetBool(SAVE_ON_PLAY_KEY, value);
    }

    /// <summary>
    /// Gets or sets whether to save before compilation
    /// </summary>
    private static bool SaveOnCompile {
      get => EditorPrefs.GetBool(SAVE_ON_COMPILE_KEY, true);
      set => EditorPrefs.SetBool(SAVE_ON_COMPILE_KEY, value);
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
    /// Gets or sets the backup locations
    /// </summary>
    private static string[] BackupLocations {
      get => EditorPrefs.GetString(BACKUP_LOCATIONS_KEY, "").Split('|', StringSplitOptions.RemoveEmptyEntries);
      set => EditorPrefs.SetString(BACKUP_LOCATIONS_KEY, string.Join("|", value));
    }

    /// <summary>
    /// Gets or sets whether to compress saves
    /// </summary>
    private static bool CompressSaves {
      get => EditorPrefs.GetBool(COMPRESS_SAVES_KEY, DEFAULT_COMPRESS_SAVES);
      set => EditorPrefs.SetBool(COMPRESS_SAVES_KEY, value);
    }

    /// <summary>
    /// Gets or sets whether to save metadata
    /// </summary>
    private static bool SaveMetadata {
      get => EditorPrefs.GetBool(SAVE_METADATA_KEY, true);
      set => EditorPrefs.SetBool(SAVE_METADATA_KEY, value);
    }

    /// <summary>
    /// Gets or sets whether to save all open scenes
    /// </summary>
    private static bool SaveAllScenes {
      get => EditorPrefs.GetBool(SAVE_ALL_SCENES_KEY, DEFAULT_SAVE_ALL_SCENES);
      set => EditorPrefs.SetBool(SAVE_ALL_SCENES_KEY, value);
    }

    /// <summary>
    /// Gets or sets whether to save scene dependencies
    /// </summary>
    private static bool SaveDependencies {
      get => EditorPrefs.GetBool(SAVE_DEPENDENCIES_KEY, DEFAULT_SAVE_DEPENDENCIES);
      set => EditorPrefs.SetBool(SAVE_DEPENDENCIES_KEY, value);
    }

    /// <summary>
    /// Gets or sets the scene groups
    /// </summary>
    private static Dictionary<string, List<string>> SceneGroups {
      get {
        var groups = new Dictionary<string, List<string>>();
        string[] groupData = EditorPrefs.GetString(SCENE_GROUPS_KEY, "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string group in groupData) {
          string[] parts = group.Split(':');
          if (parts.Length == 2) {
            groups[parts[0]] = parts[1].Split(',').ToList();
          }
        }
        return groups;
      }
      set {
        var groupData = value.Select(g => $"{g.Key}:{string.Join(",", g.Value)}");
        EditorPrefs.SetString(SCENE_GROUPS_KEY, string.Join(";", groupData));
      }
    }

    /// <summary>
    /// Gets or sets scene-specific settings
    /// </summary>
    private static Dictionary<string, SceneSettings> SceneSettingsData {
      get {
        var settings = new Dictionary<string, SceneSettings>();
        string[] data = EditorPrefs.GetString(SCENE_SETTINGS_KEY, "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in data) {
          string[] parts = entry.Split(':');
          if (parts.Length == 2) {
            var sceneSettings = new SceneSettings();
            string[] values = parts[1].Split(',');
            if (values.Length >= 4) {
              sceneSettings.Enabled = bool.Parse(values[0]);
              sceneSettings.CustomInterval = int.Parse(values[1]);
              sceneSettings.SaveOnPlay = bool.Parse(values[2]);
              sceneSettings.SaveOnCompile = bool.Parse(values[3]);
              if (values.Length > 4) sceneSettings.ExcludedAssets = values[4].Split('|');
              if (values.Length > 5) sceneSettings.RequiredAssets = values[5].Split('|');
            }
            settings[parts[0]] = sceneSettings;
          }
        }
        return settings;
      }
      set {
        var data = value.Select(kvp => 
          $"{kvp.Key}:{kvp.Value.Enabled},{kvp.Value.CustomInterval},{kvp.Value.SaveOnPlay}," +
          $"{kvp.Value.SaveOnCompile}," +
          $"{string.Join("|", kvp.Value.ExcludedAssets)}," +
          $"{string.Join("|", kvp.Value.RequiredAssets)}");
        EditorPrefs.SetString(SCENE_SETTINGS_KEY, string.Join(";", data));
      }
    }

    /// <summary>
    /// Gets or sets custom triggers
    /// </summary>
    private static List<CustomTrigger> CustomTriggers {
      get {
        var triggers = new List<CustomTrigger>();
        string[] data = EditorPrefs.GetString(CUSTOM_TRIGGERS_KEY, "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in data) {
          string[] parts = entry.Split(':');
          if (parts.Length == 4) {
            triggers.Add(new CustomTrigger {
              Name = parts[0],
              Type = (TriggerType)Enum.Parse(typeof(TriggerType), parts[1]),
              Condition = parts[2],
              Enabled = bool.Parse(parts[3])
            });
          }
        }
        return triggers;
      }
      set {
        var data = value.Select(t => $"{t.Name}:{t.Type}:{t.Condition}:{t.Enabled}");
        EditorPrefs.SetString(CUSTOM_TRIGGERS_KEY, string.Join(";", data));
      }
    }

    /// <summary>
    /// Gets or sets the backup strategy
    /// </summary>
    private static BackupStrategy BackupStrategyType {
      get => (BackupStrategy)EditorPrefs.GetInt(BACKUP_STRATEGY_KEY, (int)BackupStrategy.Rotating);
      set => EditorPrefs.SetInt(BACKUP_STRATEGY_KEY, (int)value);
    }

    /// <summary>
    /// Gets or sets whether validation is enabled
    /// </summary>
    private static bool ValidationEnabled {
      get => EditorPrefs.GetBool(VALIDATION_ENABLED_KEY, DEFAULT_VALIDATION_ENABLED);
      set => EditorPrefs.SetBool(VALIDATION_ENABLED_KEY, value);
    }

    /// <summary>
    /// Gets or sets whether to show notifications
    /// </summary>
    private static bool ShowNotifications {
      get => EditorPrefs.GetBool(SHOW_NOTIFICATIONS_KEY, DEFAULT_SHOW_NOTIFICATIONS);
      set => EditorPrefs.SetBool(SHOW_NOTIFICATIONS_KEY, value);
    }

    /// <summary>
    /// Gets or sets the notification duration
    /// </summary>
    private static float NotificationDuration {
      get => EditorPrefs.GetFloat(NOTIFICATION_DURATION_KEY, DEFAULT_NOTIFICATION_DURATION);
      set => EditorPrefs.SetFloat(NOTIFICATION_DURATION_KEY, value);
    }

    /// <summary>
    /// Gets or sets whether to show the timeline
    /// </summary>
    private static bool ShowTimeline {
      get => EditorPrefs.GetBool(SHOW_TIMELINE_KEY, DEFAULT_SHOW_TIMELINE);
      set => EditorPrefs.SetBool(SHOW_TIMELINE_KEY, value);
    }

    /// <summary>
    /// Called on editor update
    /// </summary>
    private static void OnEditorUpdate() {
      if (!IsAutosaveEnabled() || _isPlayMode) return;

      var window = GetWindow<Sceneautosave>();
      if (window != null) {
        window.UpdateTimer();
      }
    }

    /// <summary>
    /// Called when play mode state changes
    /// </summary>
    private static void OnPlayModeStateChanged(PlayModeStateChange state) {
      _isPlayMode = state == PlayModeStateChange.EnteredPlayMode;
      
      if (state == PlayModeStateChange.ExitingEditMode && SaveOnPlay) {
        var window = GetWindow<Sceneautosave>();
        if (window != null) {
          window.SaveScene();
        }
      }
    }

    /// <summary>
    /// Called before assembly reload
    /// </summary>
    private static void OnBeforeAssemblyReload() {
      if (SaveOnCompile) {
        var window = GetWindow<Sceneautosave>();
        if (window != null) {
          window.SaveScene();
        }
      }
    }

    /// <summary>
    /// Called after assembly reload
    /// </summary>
    private static void OnAfterAssemblyReload() {
    }

    /// <summary>
    /// Saves all open scenes
    /// </summary>
    private void SaveAllOpenScenes() {
      Scene[] openScenes = new Scene[SceneManager.sceneCount];
      for (int i = 0; i < SceneManager.sceneCount; i++) {
        openScenes[i] = SceneManager.GetSceneAt(i);
      }

      foreach (Scene scene in openScenes) {
        if (!string.IsNullOrEmpty(scene.path)) {
          SaveSceneInternal(scene);
        }
      }
    }

    /// <summary>
    /// Saves the current scene
    /// </summary>
    private void SaveCurrentScene() {
      Scene currentScene = SceneManager.GetActiveScene();
      if (!string.IsNullOrEmpty(currentScene.path)) {
        SaveSceneInternal(currentScene);
      }
    }

    /// <summary>
    /// Updates the timer countdown
    /// </summary>
    private void UpdateTimer() {
      bool isEnabled = IsAutosaveEnabled();
      
      // Reset timer when enabling
      if (isEnabled && !_wasEnabled) {
        _nextSaveTime = EditorApplication.timeSinceStartup + AutoSaveInterval;
      }
      
      // Only update if enabled and not in play mode
      if (isEnabled && !_isPlayMode) {
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime >= _nextSaveTime) {
          SaveScene();
          _nextSaveTime = currentTime + AutoSaveInterval;
        }
      }
      
      _wasEnabled = isEnabled;
    }

    /// <summary>
    /// Menu item to open the autosave window
    /// </summary>
    [MenuItem("Redline/Scene AutoSave", false, 500)]
    private static void Init() {
      var window = (Sceneautosave)GetWindow(typeof(Sceneautosave));
      window.Show();
    }

    /// <summary>
    /// Called when the window is enabled
    /// </summary>
    public void OnEnable() {
      titleContent = new GUIContent("Auto Save");
      minSize = new Vector2(400, 200);
      
      // Initialize timer when window opens
      if (!_wasEnabled && IsAutosaveEnabled()) {
        _nextSaveTime = EditorApplication.timeSinceStartup + AutoSaveInterval;
      }
      _wasEnabled = IsAutosaveEnabled();

      // Initialize backup locations
      _backupLocations = new List<string>(BackupLocations);
      _compressSaves = CompressSaves;
      _saveMetadata = SaveMetadata;

      // Initialize scene management settings
      _saveAllScenes = SaveAllScenes;
      _sceneGroups = new Dictionary<string, List<string>>(SceneGroups);

      // Initialize advanced settings
      _sceneSettings = new Dictionary<string, SceneSettings>(SceneSettingsData);
      _customTriggers = new List<CustomTrigger>(CustomTriggers);
      _backupStrategy = BackupStrategyType;
      _validationEnabled = ValidationEnabled;
    }

    /// <summary>
    /// Called when the window is drawn
    /// </summary>
    private void OnGUI() {
      // Begin scroll view
      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

      // Main settings
      EditorGUILayout.LabelField("Autosave Settings", EditorStyles.boldLabel);
      
      // Enable/disable toggle
      bool isEnabled = EditorGUILayout.Toggle("Enable Autosave", IsAutosaveEnabled());
      if (isEnabled != IsAutosaveEnabled()) {
        EditorPrefs.SetBool(AUTOSAVE_ENABLED_KEY, isEnabled);
      }
      
      // Interval slider
      int newInterval = EditorGUILayout.IntSlider("Autosave Interval (seconds)", AutoSaveInterval, MIN_INTERVAL, MAX_INTERVAL);
      if (newInterval != AutoSaveInterval) {
        AutoSaveInterval = newInterval;
      }

      // Countdown timer
      if (IsAutosaveEnabled() && !_isPlayMode) {
        int timeToSave = Mathf.Clamp((int)(_nextSaveTime - EditorApplication.timeSinceStartup), 0, AutoSaveInterval);
        int minutes = timeToSave / 60;
        int seconds = timeToSave % 60;
        EditorGUILayout.LabelField($"Time until next save: {minutes:00}:{seconds:00}");
      }
      
      // Save on play toggle
      bool newSaveOnPlay = EditorGUILayout.Toggle("Save on Play", SaveOnPlay);
      if (newSaveOnPlay != SaveOnPlay) {
        SaveOnPlay = newSaveOnPlay;
      }
      
      // Save on compile toggle
      bool newSaveOnCompile = EditorGUILayout.Toggle("Save on Compile", SaveOnCompile);
      if (newSaveOnCompile != SaveOnCompile) {
        SaveOnCompile = newSaveOnCompile;
      }
      
      EditorGUILayout.Space();
      
      // Backup settings
      EditorGUILayout.LabelField("Backup Settings", EditorStyles.boldLabel);
      
      // Prefix
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
      
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Update Excluded Scenes")) {
        ExcludedScenes = _excludedScenesText.Split(',', StringSplitOptions.RemoveEmptyEntries)
          .Select(s => s.Trim())
          .ToArray();
      }
      
      // Add button to exclude current scene
      Scene currentScene = SceneManager.GetActiveScene();
      if (!string.IsNullOrEmpty(currentScene.path)) {
        string currentSceneName = Path.GetFileNameWithoutExtension(currentScene.path);
        if (GUILayout.Button("Exclude Current Scene")) {
          var excludedList = ExcludedScenes.ToList();
          if (!excludedList.Contains(currentSceneName)) {
            excludedList.Add(currentSceneName);
            ExcludedScenes = excludedList.ToArray();
            _excludedScenesText = string.Join(", ", ExcludedScenes);
          }
        }
      }
      EditorGUILayout.EndHorizontal();
      
      EditorGUILayout.Space();

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Scene Management", EditorStyles.boldLabel);
      
      // Save all scenes toggle
      _saveAllScenes = EditorGUILayout.Toggle("Save All Open Scenes", _saveAllScenes);
      if (_saveAllScenes != SaveAllScenes) {
        SaveAllScenes = _saveAllScenes;
      }
      
      // Scene groups
      _showSceneGroups = EditorGUILayout.Foldout(_showSceneGroups, "Scene Groups", true);
      if (_showSceneGroups) {
        DrawSceneGroups();
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Advanced Configuration", EditorStyles.boldLabel);
      
      // Scene settings
      _showSceneSettings = EditorGUILayout.Foldout(_showSceneSettings, "Scene-Specific Settings", true);
      if (_showSceneSettings) {
        DrawSceneSettings();
      }
      
      // Custom triggers
      _showCustomTriggers = EditorGUILayout.Foldout(_showCustomTriggers, "Custom Save Triggers", true);
      if (_showCustomTriggers) {
        DrawCustomTriggers();
      }
      
      // Backup settings
      _showBackupSettings = EditorGUILayout.Foldout(_showBackupSettings, "Backup Settings", true);
      if (_showBackupSettings) {
        DrawBackupSettings();
      }
      
      // Validation settings
      _showValidationSettings = EditorGUILayout.Foldout(_showValidationSettings, "Validation Settings", true);
      if (_showValidationSettings) {
        DrawValidationSettings();
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("User Experience", EditorStyles.boldLabel);
      
      // Notification settings
      _showNotifications = EditorGUILayout.Toggle("Show Notifications", _showNotifications);
      if (_showNotifications != ShowNotifications) {
        ShowNotifications = _showNotifications;
      }
      
      if (_showNotifications) {
        EditorGUI.indentLevel++;
        _notificationDuration = EditorGUILayout.Slider("Notification Duration", _notificationDuration, 1f, 10f);
        if (_notificationDuration != NotificationDuration) {
          NotificationDuration = _notificationDuration;
        }
        EditorGUI.indentLevel--;
      }
      
      // Timeline settings
      _showTimeline = EditorGUILayout.Toggle("Show Timeline", _showTimeline);
      if (_showTimeline != ShowTimeline) {
        ShowTimeline = _showTimeline;
      }
      
      if (_showTimeline) {
        DrawTimeline();
      }

      // End scroll view
      EditorGUILayout.EndScrollView();
    }
    
    /// <summary>
    /// Draws the scene groups UI
    /// </summary>
    private void DrawSceneGroups() {
      EditorGUI.indentLevel++;
      
      // Add new group
      EditorGUILayout.BeginHorizontal();
      string newGroupName = EditorGUILayout.TextField("New Group Name");
      if (GUILayout.Button("Add Group", GUILayout.Width(80))) {
        if (!string.IsNullOrEmpty(newGroupName) && !_sceneGroups.ContainsKey(newGroupName)) {
          _sceneGroups[newGroupName] = new List<string>();
          SceneGroups = _sceneGroups;
        }
      }
      EditorGUILayout.EndHorizontal();
      
      // Existing groups
      foreach (var group in _sceneGroups.ToList()) {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
        if (GUILayout.Button("Remove", GUILayout.Width(80))) {
          _sceneGroups.Remove(group.Key);
          SceneGroups = _sceneGroups;
          continue;
        }
        EditorGUILayout.EndHorizontal();
        
        // Scene list
        for (int i = 0; i < group.Value.Count; i++) {
          EditorGUILayout.BeginHorizontal();
          group.Value[i] = EditorGUILayout.TextField(group.Value[i]);
          if (GUILayout.Button("Browse", GUILayout.Width(60))) {
            string path = EditorUtility.OpenFilePanel("Select Scene", "Assets", "unity");
            if (!string.IsNullOrEmpty(path)) {
              group.Value[i] = path;
            }
          }
          if (GUILayout.Button("X", GUILayout.Width(20))) {
            group.Value.RemoveAt(i);
            i--;
          }
          EditorGUILayout.EndHorizontal();
        }
        
        // Add scene to group
        if (GUILayout.Button("Add Scene")) {
          string path = EditorUtility.OpenFilePanel("Select Scene", "Assets", "unity");
          if (!string.IsNullOrEmpty(path)) {
            group.Value.Add(path);
          }
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
      }
      
      EditorGUI.indentLevel--;
    }
    
    /// <summary>
    /// Draws the scene settings UI
    /// </summary>
    private void DrawSceneSettings() {
      EditorGUI.indentLevel++;
      
      // Scene selector
      string[] scenes = _sceneSettings.Keys.ToArray();
      if (scenes.Length > 0) {
        int selectedIndex = EditorGUILayout.Popup("Select Scene", 
          Array.IndexOf(scenes, _selectedScene), scenes);
        if (selectedIndex >= 0) {
          _selectedScene = scenes[selectedIndex];
          var settings = _sceneSettings[_selectedScene];
          
          settings.Enabled = EditorGUILayout.Toggle("Enabled", settings.Enabled);
          settings.CustomInterval = EditorGUILayout.IntField("Custom Interval (seconds)", 
            settings.CustomInterval);
          settings.SaveOnPlay = EditorGUILayout.Toggle("Save on Play", settings.SaveOnPlay);
          settings.SaveOnCompile = EditorGUILayout.Toggle("Save on Compile", settings.SaveOnCompile);
          
          // Excluded assets
          EditorGUILayout.LabelField("Excluded Assets");
          for (int i = 0; i < settings.ExcludedAssets.Length; i++) {
            EditorGUILayout.BeginHorizontal();
            settings.ExcludedAssets[i] = EditorGUILayout.TextField(settings.ExcludedAssets[i]);
            if (GUILayout.Button("Browse", GUILayout.Width(60))) {
              string path = EditorUtility.OpenFilePanel("Select Asset", "Assets", "");
              if (!string.IsNullOrEmpty(path)) {
                settings.ExcludedAssets[i] = path;
              }
            }
            if (GUILayout.Button("X", GUILayout.Width(20))) {
              var list = settings.ExcludedAssets.ToList();
              list.RemoveAt(i);
              settings.ExcludedAssets = list.ToArray();
              i--;
            }
            EditorGUILayout.EndHorizontal();
          }
          
          // Required assets
          EditorGUILayout.LabelField("Required Assets");
          for (int i = 0; i < settings.RequiredAssets.Length; i++) {
            EditorGUILayout.BeginHorizontal();
            settings.RequiredAssets[i] = EditorGUILayout.TextField(settings.RequiredAssets[i]);
            if (GUILayout.Button("Browse", GUILayout.Width(60))) {
              string path = EditorUtility.OpenFilePanel("Select Asset", "Assets", "");
              if (!string.IsNullOrEmpty(path)) {
                settings.RequiredAssets[i] = path;
              }
            }
            if (GUILayout.Button("X", GUILayout.Width(20))) {
              var list = settings.RequiredAssets.ToList();
              list.RemoveAt(i);
              settings.RequiredAssets = list.ToArray();
              i--;
            }
            EditorGUILayout.EndHorizontal();
          }
        }
      }
      
      EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Draws the custom triggers UI
    /// </summary>
    private void DrawCustomTriggers() {
      EditorGUI.indentLevel++;
      
      for (int i = 0; i < _customTriggers.Count; i++) {
        var trigger = _customTriggers[i];
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        trigger.Name = EditorGUILayout.TextField("Name", trigger.Name);
        trigger.Type = (TriggerType)EditorGUILayout.EnumPopup("Type", trigger.Type);
        trigger.Condition = EditorGUILayout.TextField("Condition", trigger.Condition);
        trigger.Enabled = EditorGUILayout.Toggle("Enabled", trigger.Enabled);
        
        if (GUILayout.Button("Remove")) {
          _customTriggers.RemoveAt(i);
          i--;
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
      }
      
      if (GUILayout.Button("Add Trigger")) {
        _customTriggers.Add(new CustomTrigger {
          Name = "New Trigger",
          Type = TriggerType.TimeBased,
          Condition = "",
          Enabled = true
        });
      }
      
      EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Draws the backup settings UI
    /// </summary>
    private void DrawBackupSettings() {
      EditorGUI.indentLevel++;
      
      _backupStrategy = (BackupStrategy)EditorGUILayout.EnumPopup("Strategy", _backupStrategy);
      
      switch (_backupStrategy) {
        case BackupStrategy.Rotating:
          EditorGUILayout.HelpBox("Keeps the N most recent backups and deletes older ones.", 
            MessageType.Info);
          break;
        case BackupStrategy.Incremental:
          EditorGUILayout.HelpBox("Keeps all backups with incremental naming (e.g., _001, _002).", 
            MessageType.Info);
          break;
        case BackupStrategy.Timestamped:
          EditorGUILayout.HelpBox("Keeps backups with timestamps in the filename.", 
            MessageType.Info);
          break;
        case BackupStrategy.Hybrid:
          EditorGUILayout.HelpBox("Combines multiple strategies for maximum flexibility.", 
            MessageType.Info);
          break;
      }
      
      EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Draws the validation settings UI
    /// </summary>
    private void DrawValidationSettings() {
      EditorGUI.indentLevel++;
      
      bool newValidationEnabled = EditorGUILayout.Toggle("Enable Validation", _validationEnabled);
      if (newValidationEnabled != _validationEnabled) {
        _validationEnabled = newValidationEnabled;
        ValidationEnabled = newValidationEnabled;  // Save to EditorPrefs
      }
      
      if (_validationEnabled) {
        EditorGUILayout.HelpBox("Validates autosaves for integrity and completeness.", 
          MessageType.Info);
      }
      
      EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Saves the current scene with enhanced features
    /// </summary>
    private void SaveScene() {
      if (_saveAllScenes) {
        SaveAllOpenScenes();
      } else {
        SaveCurrentScene();
      }
    }

    /// <summary>
    /// Internal method to save a scene with all features
    /// </summary>
    private void SaveSceneInternal(Scene scene) {
      string scenePath = scene.path;
      
      if (string.IsNullOrEmpty(scenePath)) {
        Debug.LogWarning($"Cannot autosave scene {scene.name} that hasn't been saved yet.");
        return;
      }

      if (ExcludedScenes.Contains(Path.GetFileNameWithoutExtension(scenePath))) {
        Debug.Log($"Scene {scenePath} is excluded from autosave.");
        return;
      }
      
      string directory = Path.GetDirectoryName(scenePath);
      string fileName = Path.GetFileName(scenePath);
      string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
      string autoSavePath = Path.Combine(directory, $"{AutoSavePrefix}{fileName}");
      
      try {
        // Save the scene
        EditorSceneManager.SaveScene(scene, autoSavePath, true);
        
        // Save metadata if enabled
        if (SaveMetadata) {
          SaveMetadataFile(autoSavePath, scene);
        }
        
        // Compress if enabled
        if (CompressSaves) {
          CompressSave(autoSavePath);
        }
        
        // Copy to backup locations
        foreach (string backupLocation in _backupLocations) {
          if (Directory.Exists(backupLocation)) {
            string backupPath = Path.Combine(backupLocation, $"{AutoSavePrefix}{timestamp}_{fileName}");
            if (CompressSaves) {
              backupPath += ".zip";
            }
            File.Copy(CompressSaves ? autoSavePath + ".zip" : autoSavePath, backupPath, true);
          }
        }
        
        // Only validate if autosave is enabled and validation is enabled
        if (IsAutosaveEnabled() && _validationEnabled) {
          if (!ValidateSave(autoSavePath)) {
            ShowNotification($"Failed to validate autosave for scene {scene.name}", MessageType.Error);
            return;
          }
        }
        
        Debug.Log($"Scene autosaved to: {autoSavePath}");
        _lastSaveTimes[scenePath] = DateTime.Now;
        
        // Clean up old autosaves
        CleanupOldAutosaves(directory, fileName);
      }
      catch (Exception e) {
        ShowNotification($"Failed to autosave scene {scene.name}: {e.Message}", MessageType.Error);
      }
    }

    /// <summary>
    /// Saves metadata about the scene
    /// </summary>
    private void SaveMetadataFile(string scenePath, Scene scene) {
      string metadataPath = scenePath + ".meta.json";
      var metadata = new {
        UnityVersion = Application.unityVersion,
        ProjectName = Application.productName,
        SaveTime = DateTime.Now,
        SceneName = scene.name,
        ScenePath = scene.path,
        BuildSettings = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
        Dependencies = AssetDatabase.GetDependencies(scene.path, true)
      };
      
      File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
    }

    /// <summary>
    /// Compresses a save file
    /// </summary>
    private void CompressSave(string filePath) {
      string zipPath = filePath + ".zip";
      string metadataPath = filePath + ".meta.json";
      
      using (FileStream zipToCreate = new FileStream(zipPath, FileMode.Create)) {
        using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create)) {
          // Add the scene file
          archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
          
          // Add metadata if it exists
          if (File.Exists(metadataPath)) {
            archive.CreateEntryFromFile(metadataPath, Path.GetFileName(metadataPath));
          }
        }
      }
      
      // Delete original files after compression
      File.Delete(filePath);
      if (File.Exists(metadataPath)) {
        File.Delete(metadataPath);
      }
    }

    /// <summary>
    /// Cleans up old autosave files
    /// </summary>
    private void CleanupOldAutosaves(string directory, string originalFileName) {
      try {
        string pattern = $"{AutoSavePrefix}{originalFileName}*";
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
    private static bool IsAutosaveEnabled() {
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

    /// <summary>
    /// Draws the timeline UI
    /// </summary>
    private void DrawTimeline() {
      if (!_showTimeline) return;

      EditorGUILayout.LabelField("Save Timeline", EditorStyles.boldLabel);
      
      // Scene selector
      string[] scenes = _saveHistory.Keys.ToArray();
      if (scenes.Length > 0) {
        int selectedIndex = Array.IndexOf(scenes, _selectedScene);
        int newIndex = EditorGUILayout.Popup("Select Scene", selectedIndex, scenes);
        if (newIndex != selectedIndex) {
          _selectedScene = scenes[newIndex];
          _selectedSave = null;
        }
      }
      
      if (!string.IsNullOrEmpty(_selectedScene) && _saveHistory.ContainsKey(_selectedScene)) {
        _timelineScrollPosition = EditorGUILayout.BeginScrollView(_timelineScrollPosition, GUILayout.Height(200));
        
        var saves = _saveHistory[_selectedScene];
        foreach (var save in saves.OrderByDescending(s => s.SaveTime)) {
          EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
          
          // Save info
          EditorGUILayout.BeginVertical();
          EditorGUILayout.LabelField(save.SaveTime.ToString("yyyy-MM-dd HH:mm:ss"), EditorStyles.boldLabel);
          EditorGUILayout.LabelField($"Size: {FormatFileSize(save.FileSize)}");
          if (save.HasMetadata) {
            EditorGUILayout.LabelField("Has Metadata");
          }
          EditorGUILayout.EndVertical();
          
          // Preview button
          if (GUILayout.Button("Preview", GUILayout.Width(80))) {
            _selectedSave = save;
            PreviewSave(save);
          }
          
          // Restore button
          if (GUILayout.Button("Restore", GUILayout.Width(80))) {
            if (EditorUtility.DisplayDialog("Restore Save",
                "Are you sure you want to restore this save? Current changes will be lost.",
                "Restore", "Cancel")) {
              RestoreSave(save);
            }
          }
          
          EditorGUILayout.EndHorizontal();
          EditorGUILayout.Space(5);
        }
        
        EditorGUILayout.EndScrollView();
      }
    }

    /// <summary>
    /// Formats a file size
    /// </summary>
    private string FormatFileSize(long bytes) {
      string[] sizes = { "B", "KB", "MB", "GB" };
      int order = 0;
      double size = bytes;
      
      while (size >= 1024 && order < sizes.Length - 1) {
        order++;
        size /= 1024;
      }
      
      return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Previews a save
    /// </summary>
    private void PreviewSave(SaveInfo save) {
      if (save == null) return;

      try {
        // Create a temporary copy of the save
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(save.FilePath));
        File.Copy(save.FilePath, tempPath, true);

        // Open the scene in a new window
        var previewWindow = CreateInstance<Sceneautosave>();
        previewWindow.titleContent = new GUIContent($"Preview: {Path.GetFileNameWithoutExtension(save.FilePath)}");
        previewWindow.Show();

        // Load the scene
        EditorSceneManager.OpenScene(tempPath, OpenSceneMode.Single);

        ShowNotification($"Previewing save from {save.SaveTime}", MessageType.Info);
      }
      catch (Exception e) {
        ShowNotification($"Failed to preview save: {e.Message}", MessageType.Error);
      }
    }

    /// <summary>
    /// Restores a save
    /// </summary>
    private void RestoreSave(SaveInfo save) {
      if (save == null) return;

      try {
        // Create a temporary copy of the save
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(save.FilePath));
        File.Copy(save.FilePath, tempPath, true);

        // Open the scene
        EditorSceneManager.OpenScene(tempPath);
        ShowNotification($"Restored save from {save.SaveTime}", MessageType.Info);
      }
      catch (Exception e) {
        ShowNotification($"Failed to restore save: {e.Message}", MessageType.Error);
      }
    }

    /// <summary>
    /// Validates a save file
    /// </summary>
    private bool ValidateSave(string filePath) {
      if (!_validationEnabled) return true;
      
      try {
        // Only validate autosave files
        if (!Path.GetFileName(filePath).StartsWith(AutoSavePrefix)) {
          return true;
        }
        
        // Check if file exists and is readable
        if (!File.Exists(filePath)) return false;
        
        // Check file size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0) return false;
        
        // Check if it's a valid Unity scene file
        if (filePath.EndsWith(".unity")) {
          string content = File.ReadAllText(filePath);
          if (!content.Contains("UnityScene")) return false;
        }
        
        // Check metadata if it exists
        string metadataPath = filePath + ".meta.json";
        if (File.Exists(metadataPath)) {
          string metadata = File.ReadAllText(metadataPath);
          if (string.IsNullOrEmpty(metadata)) return false;
        }
        
        return true;
      }
      catch {
        return false;
      }
    }

    /// <summary>
    /// Shows a notification
    /// </summary>
    private void ShowNotification(string message, MessageType type = MessageType.Info) {
      if (!_showNotifications) return;
      
      EditorUtility.DisplayDialog("Redline Autosave", message, "OK");
    }
  }
}
#endif
