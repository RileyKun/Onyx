using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Memory management tool for Unity Editor that monitors and controls memory usage.
/// Provides automatic cleanup, memory threshold monitoring, and system memory detection.
/// </summary>
[InitializeOnLoad]
public class MemoryManagementTool : EditorWindow
{
    // Core memory tracking
    private static float memoryUsageMB = 0;
    private static float peakMemoryUsageMB = 0;
    private static float lastCleanupTime = 0;
    private static float cleanupIntervalSeconds = 300; // 5 minutes between timer-based cleanups
    private static bool autoCleanupEnabled = false;
    private static float memoryThresholdMB = 2048;
    private static bool initializedSystemMemory = false;
    private static bool initializedSettings = false;
    
    // Memory limit presets
    // TODO: Why the hell are you using strings to index this array? This is slow.
    private static Dictionary<string, float> memoryPresets = new Dictionary<string, float>()
    {
        {"Very Low-End (1GB Limit)", 1024},
        {"Low-End (2GB Limit)", 2048},
        {"Average (4GB Limit)", 4096},
        {"High-End (8GB Limit)", 8192},
        {"Auto (50% of physical RAM)", 0},
        {"Auto (50% of RAM + Swap)", 0},
        {"Custom", 0}
    };
    private static string selectedPreset = "Auto (50% of physical RAM)";
    private static Vector2 scrollPosition;
    
    // EditorPrefs keys for settings persistence
    private const string PREF_SELECTED_PRESET = "Redline_MemoryManager_SelectedPreset";
    private const string PREF_MEMORY_THRESHOLD = "Redline_MemoryManager_Threshold";
    private const string PREF_AUTO_CLEANUP = "Redline_MemoryManager_AutoCleanup";
    private const string PREF_CLEANUP_INTERVAL = "Redline_MemoryManager_CleanupInterval";
    private const string PREF_SHOW_GRAPH = "Redline_MemoryManager_ShowGraph";
    private const string PREF_LAST_CLEANUP_TIME = "Redline_MemoryManager_LastCleanupTime";
    private const string PREF_PEAK_MEMORY = "Redline_MemoryManager_PeakMemory";
    private const string PREF_CUSTOM_MEMORY = "Redline_MemoryManager_CustomMemory";
    
    // Memory usage graph data
    private static List<float> memoryHistory = new List<float>();
    private static int maxHistoryPoints = 60;
    private static bool showGraph = true;
    private static float graphUpdateInterval = 1.0f;
    private static float lastGraphUpdateTime = 0;
    
    // System memory information
    private static float totalSystemMemoryMB = 0;
    private static float totalPhysicalMemoryMB = 0;
    private static float totalSwapMB = 0;
    
    // Windows API for physical memory detection
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
    
    /// <summary>
    /// Static constructor - sets up event handlers and initializes the tool
    /// </summary>
    static MemoryManagementTool()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        HookVRChatEvents();
    }
    
    /// <summary>
    /// Called when the window is enabled - loads settings and initializes memory detection
    /// </summary>
    private void OnEnable()
    {
        if (!initializedSettings)
        {
            LoadSettings();
            initializedSettings = true;
        }
        
        if (!initializedSystemMemory)
        {
            DetectSystemMemory();
            initializedSystemMemory = true;
        }
    }
    
    /// <summary>
    /// Loads all saved settings from EditorPrefs
    /// </summary>
    private static void LoadSettings()
    {
        // Load saved preset
        string savedPreset = EditorPrefs.GetString(PREF_SELECTED_PRESET, "Auto (50% of physical RAM)");
        if (memoryPresets.ContainsKey(savedPreset))
        {
            selectedPreset = savedPreset;
        }
        
        // Load saved threshold
        if (selectedPreset == "Custom")
        {
            memoryThresholdMB = EditorPrefs.GetFloat(PREF_CUSTOM_MEMORY, memoryThresholdMB);
        }
        else
        {
            memoryThresholdMB = memoryPresets[selectedPreset];
        }
        
        // Load auto cleanup settings
        autoCleanupEnabled = EditorPrefs.GetBool(PREF_AUTO_CLEANUP, false);
        cleanupIntervalSeconds = EditorPrefs.GetFloat(PREF_CLEANUP_INTERVAL, 300f);
        
        // Load UI preferences
        showGraph = EditorPrefs.GetBool(PREF_SHOW_GRAPH, true);
        
        // Load last cleanup time and peak memory
        lastCleanupTime = EditorPrefs.GetFloat(PREF_LAST_CLEANUP_TIME, 0f);
        peakMemoryUsageMB = EditorPrefs.GetFloat(PREF_PEAK_MEMORY, 0f);
    }
    
    /// <summary>
    /// Saves all current settings to EditorPrefs
    /// </summary>
    private static void SaveSettings()
    {
        EditorPrefs.SetString(PREF_SELECTED_PRESET, selectedPreset);
        if (selectedPreset == "Custom")
        {
            EditorPrefs.SetFloat(PREF_CUSTOM_MEMORY, memoryThresholdMB);
        }
        EditorPrefs.SetFloat(PREF_MEMORY_THRESHOLD, memoryThresholdMB);
        EditorPrefs.SetBool(PREF_AUTO_CLEANUP, autoCleanupEnabled);
        EditorPrefs.SetFloat(PREF_CLEANUP_INTERVAL, cleanupIntervalSeconds);
        EditorPrefs.SetBool(PREF_SHOW_GRAPH, showGraph);
        EditorPrefs.SetFloat(PREF_LAST_CLEANUP_TIME, lastCleanupTime);
        EditorPrefs.SetFloat(PREF_PEAK_MEMORY, peakMemoryUsageMB);
    }
    
    /// <summary>
    /// Detects system memory information for the current platform
    /// </summary>
    private static void DetectSystemMemory()
    {
        try
        {
            totalPhysicalMemoryMB = 0;
            totalSwapMB = 0;
            totalSystemMemoryMB = 0;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DetectWindowsMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                DetectLinuxMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                DetectMacOSMemory();
            }
            
            // 1024 is our bare minimum with configurations, don't let the total drop below this.
            if (totalPhysicalMemoryMB < 1024)
            {
                UnityEngine.Debug.LogWarning("[Redline Memory Master] Total memory amount was found to be below 1024MiB... How are you even running this?");
                totalPhysicalMemoryMB = totalSystemMemoryMB = 1024;
                UnityEngine.Debug.LogWarning("[Redline Memory Master] Forcing memory amount to 1024.");
            }
            
            // Update the auto preset values
            memoryPresets["Auto (50% of physical RAM)"] = totalPhysicalMemoryMB / 2;
            memoryPresets["Auto (50% of RAM + Swap)"] = totalSystemMemoryMB / 2;
            
            // Set the initial threshold based on the auto value
            if (selectedPreset == "Auto (50% of physical RAM)")
            {
                memoryThresholdMB = memoryPresets[selectedPreset];
            }
            else if (selectedPreset == "Auto (50% of RAM + Swap)")
            {
                memoryThresholdMB = memoryPresets[selectedPreset];
            }
            
            UnityEngine.Debug.Log($"[Redline Memory Master] Detected physical RAM: {totalPhysicalMemoryMB:F0}MB, swap: {totalSwapMB:F0}MB, total: {totalSystemMemoryMB:F0}MB");
            initializedSystemMemory = true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[Redline Memory Master] Error detecting system memory: {e.Message}");
            
            // Fallback values
            totalPhysicalMemoryMB = 8192;
            totalSystemMemoryMB = totalPhysicalMemoryMB;
            totalSwapMB = 0;
            memoryPresets["Auto (50% of physical RAM)"] = 4096;
            memoryPresets["Auto (50% of RAM + Swap)"] = 4096;
            if (selectedPreset == "Auto (50% of physical RAM)" || selectedPreset == "Auto (50% of RAM + Swap)")
            {
                memoryThresholdMB = 4096;
            }
            initializedSystemMemory = true;
        }
    }
    
    /// <summary>
    /// Detects memory information on Windows systems
    /// </summary>
    private static void DetectWindowsMemory()
    {
        long memoryKb;
        // Anyone ever heard of TotalPhysicalMemory or TotalVirtualMemory? This is a disgrace...
        if (GetPhysicallyInstalledSystemMemory(out memoryKb))
        {
            totalPhysicalMemoryMB = memoryKb / 1024f;
            totalSystemMemoryMB = totalPhysicalMemoryMB;
            
            // Get page file info
            Process process = new Process();
            process.StartInfo.FileName = "wmic";
            process.StartInfo.Arguments = "pagefile list /format:list";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            if (output.Contains("AllocatedBaseSize"))
            {
                int index = output.IndexOf("AllocatedBaseSize=");
                if (index >= 0)
                {
                    string sizeStr = output.Substring(index + "AllocatedBaseSize=".Length);
                    int endIndex = sizeStr.IndexOf('\r');
                    if (endIndex >= 0)
                    {
                        sizeStr = sizeStr.Substring(0, endIndex);
                        int pageFileMB;
                        if (int.TryParse(sizeStr, out pageFileMB))
                        {
                            totalSwapMB = pageFileMB;
                            totalSystemMemoryMB += totalSwapMB;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Detects memory information on Linux systems
    /// </summary>
    private static void DetectLinuxMemory()
    {
        Process process = new Process();
        process.StartInfo.FileName = "free";
        process.StartInfo.Arguments = "-m";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        
        string[] lines = output.Split('\n');
        if (lines.Length >= 2)
        {
            string memLine = lines[1];
            string[] parts = memLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                float.TryParse(parts[1], out totalPhysicalMemoryMB);
                totalSystemMemoryMB = totalPhysicalMemoryMB;
                
                // Get swap information
                if (lines.Length >= 3)
                {
                    string swapLine = lines[2];
                    parts = swapLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        float swapMB;
                        if (float.TryParse(parts[1], out swapMB))
                        {
                            totalSwapMB = swapMB;
                            totalSystemMemoryMB += totalSwapMB;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Detects memory information on macOS systems
    /// </summary>
    private static void DetectMacOSMemory()
    {
        Process process = new Process();
        process.StartInfo.FileName = "sysctl";
        process.StartInfo.Arguments = "-n hw.memsize";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        
        if (!string.IsNullOrEmpty(output))
        {
            long memoryBytes;
            if (long.TryParse(output.Trim(), out memoryBytes))
            {
                totalPhysicalMemoryMB = memoryBytes / (1024f * 1024f);
                totalSystemMemoryMB = totalPhysicalMemoryMB;
                
                // Get swap information
                process = new Process();
                process.StartInfo.FileName = "sysctl";
                process.StartInfo.Arguments = "-n vm.swapusage";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                if (output.Contains("total ="))
                {
                    int startIdx = output.IndexOf("total = ") + "total = ".Length;
                    int endIdx = output.IndexOf("M", startIdx);
                    if (endIdx > startIdx)
                    {
                        string swapSizeStr = output.Substring(startIdx, endIdx - startIdx);
                        float swapSizeMB;
                        if (float.TryParse(swapSizeStr, out swapSizeMB))
                        {
                            totalSwapMB = swapSizeMB;
                            totalSystemMemoryMB += totalSwapMB;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Attempts to hook into VRChat SDK events for automatic cleanup
    /// </summary>
    private static void HookVRChatEvents()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name.Contains("VRC.SDK") || assembly.GetName().Name.Contains("VRCSDK"))
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Name.Contains("Builder") || type.Name.Contains("SDK"))
                        {
                            UnityEngine.Debug.Log($"[Redline Memory Master] Found VRChat SDK type: {type.FullName}");
                            
                            var events = type.GetEvents();
                            foreach (var evt in events)
                            {
                                if (evt.Name.Contains("Begin") || evt.Name.Contains("Start") || 
                                    evt.Name.Contains("Pre") || evt.Name.Contains("Before"))
                                {
                                    UnityEngine.Debug.Log($"[Redline Memory Master] Found VRChat SDK event: {evt.Name}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Redline Memory Master] Could not hook into VRChat SDK events: {e.Message}");
        }
    }
    
    /// <summary>
    /// Called when a scene is opened - performs memory cleanup
    /// </summary>
    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        UnityEngine.Debug.Log($"[Redline Memory Master] Scene opened: {scene.name}, cleaning up memory...");
        PerformCleanup();
    }
    
    /// <summary>
    /// Called when a scene is closed - performs memory cleanup
    /// </summary>
    private static void OnSceneClosed(UnityEngine.SceneManagement.Scene scene)
    {
        UnityEngine.Debug.Log($"[Redline Memory Master] Scene closed: {scene.name}, cleaning up memory...");
        PerformCleanup();
    }
    
    /// <summary>
    /// Shows the Memory Master window
    /// </summary>
    [MenuItem("Redline/Memory Management")]
    public static void ShowWindow()
    {
        GetWindow<MemoryManagementTool>("Memory Master");
    }
    
    /// <summary>
    /// Called every editor update - handles memory monitoring and cleanup
    /// </summary>
    private static void OnEditorUpdate()
    {
        // Update memory usage info
        memoryUsageMB = GC.GetTotalMemory(false) / (1024f * 1024f);
        
        // Update peak memory
        if (memoryUsageMB > peakMemoryUsageMB)
        {
            peakMemoryUsageMB = memoryUsageMB;
            SaveSettings();
        }
        
        // Update graph data
        if (EditorApplication.timeSinceStartup - lastGraphUpdateTime > graphUpdateInterval)
        {
            memoryHistory.Add(memoryUsageMB);
            if (memoryHistory.Count > maxHistoryPoints)
            {
                memoryHistory.RemoveAt(0);
            }
            lastGraphUpdateTime = (float)EditorApplication.timeSinceStartup;
        }
        
        // Check if we need to auto-cleanup based on memory threshold
        if (memoryUsageMB > memoryThresholdMB)
        {
            PerformCleanup();
            lastCleanupTime = (float)EditorApplication.timeSinceStartup;
        }
        // Check if we need to auto-cleanup based on timer (if enabled)
        else if (autoCleanupEnabled && 
            (EditorApplication.timeSinceStartup - lastCleanupTime > cleanupIntervalSeconds))
        {
            PerformCleanup();
            lastCleanupTime = (float)EditorApplication.timeSinceStartup;
        }
    }
    
    /// <summary>
    /// Draws the Memory Master window UI
    /// </summary>
    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Label("Redline Memory Master", EditorStyles.boldLabel);
        
        // System memory info
        // TODO: Might be best to remove these if statements.
        if (totalPhysicalMemoryMB > 0)
        {
            EditorGUILayout.LabelField($"Physical RAM: {totalPhysicalMemoryMB:F0} MB");
        }
        if (totalSwapMB > 0)
        {
            EditorGUILayout.LabelField($"Swap/Pagefile: {totalSwapMB:F0} MB");
        }
        if (totalSystemMemoryMB > 0)
        {
            EditorGUILayout.LabelField($"System RAM + Swap: {totalSystemMemoryMB:F0} MB");
        }
        
        // Display current memory usage
        EditorGUILayout.LabelField($"Current Unity Memory Usage: {memoryUsageMB:F2} MB");
        EditorGUILayout.LabelField($"Peak Memory Usage: {peakMemoryUsageMB:F2} MB");
        
        // Reset peak button
        if (GUILayout.Button("Reset Peak"))
        {
            peakMemoryUsageMB = memoryUsageMB;
            SaveSettings();
        }
        
        // Progress bar for memory usage
        float memoryPercentage = memoryUsageMB / memoryThresholdMB;
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), 
            Mathf.Clamp01(memoryPercentage), 
            $"{memoryUsageMB:F0}/{memoryThresholdMB:F0} MB ({(memoryPercentage * 100):F0}%)");
        
        // Manual cleanup button
        if (GUILayout.Button("Force Memory Cleanup"))
        {
            PerformCleanup();
            lastCleanupTime = (float)EditorApplication.timeSinceStartup;
            SaveSettings();
        }
        
        EditorGUILayout.Space();
        
        // Memory graph
        bool newShowGraph = EditorGUILayout.Foldout(showGraph, "Memory Usage Graph", true);
        if (newShowGraph != showGraph)
        {
            showGraph = newShowGraph;
            SaveSettings();
        }
        if (showGraph && memoryHistory.Count > 0)
        {
            Rect graphRect = EditorGUILayout.GetControlRect(false, 100);
            DrawMemoryGraph(graphRect);
        }
        
        EditorGUILayout.Space();
        
        // Memory limit settings
        EditorGUILayout.LabelField("Memory Limit Settings", EditorStyles.boldLabel);
        
        // Preset selection
        string[] presetNames = new string[memoryPresets.Count];
        memoryPresets.Keys.CopyTo(presetNames, 0);
        
        int currentPresetIndex = Array.IndexOf(presetNames, selectedPreset);
        int newPresetIndex = EditorGUILayout.Popup("Memory Preset", currentPresetIndex, presetNames);
        
        if (newPresetIndex != currentPresetIndex)
        {
            selectedPreset = presetNames[newPresetIndex];
            if (selectedPreset != "Custom")
            {
                memoryThresholdMB = memoryPresets[selectedPreset];
            }
            SaveSettings();
        }
        
        // Custom memory input (only show when Custom is selected)
        if (selectedPreset == "Custom")
        {
            float minMemory = 1024; // 1GB minimum
            float maxMemory = Mathf.Max(totalSystemMemoryMB, 16384); // Use total system memory or 16GB as max
            
            EditorGUILayout.BeginHorizontal();
            float newThreshold = EditorGUILayout.Slider(memoryThresholdMB, minMemory, maxMemory);
            string thresholdStr = EditorGUILayout.TextField(newThreshold.ToString("F0"), GUILayout.Width(60));
            EditorGUILayout.LabelField("MB", GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
            
            // Try to parse the text input
            float parsedThreshold;
            if (float.TryParse(thresholdStr, out parsedThreshold))
            {
                newThreshold = Mathf.Clamp(parsedThreshold, minMemory, maxMemory);
            }
            
            if (newThreshold != memoryThresholdMB)
            {
                memoryThresholdMB = newThreshold;
                SaveSettings();
            }
        }
        
        // Auto cleanup settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto Cleanup Settings", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("Memory threshold cleanup is always active. Timer-based cleanup can be enabled/disabled below.", MessageType.Info);
        
        bool newAutoCleanup = EditorGUILayout.Toggle("Enable Timer-Based Cleanup", autoCleanupEnabled);
        if (newAutoCleanup != autoCleanupEnabled)
        {
            autoCleanupEnabled = newAutoCleanup;
            SaveSettings();
        }
        
        float newInterval = EditorGUILayout.FloatField("Cleanup Interval (seconds)", cleanupIntervalSeconds);
        if (newInterval != cleanupIntervalSeconds)
        {
            cleanupIntervalSeconds = newInterval;
            SaveSettings();
        }
        
        // Display time until next cleanup
        if (autoCleanupEnabled)
        {
            float timeUntilNextCleanup = cleanupIntervalSeconds - ((float)EditorApplication.timeSinceStartup - lastCleanupTime);
            if (timeUntilNextCleanup > 0)
            {
                EditorGUILayout.LabelField($"Time until next timer-based cleanup: {timeUntilNextCleanup:F0} seconds");
            }
            else
            {
                EditorGUILayout.LabelField("Timer-based cleanup will run when interval is reached");
            }
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last Cleanup: " + (lastCleanupTime > 0 ? 
            DateTime.Now.AddSeconds(-(EditorApplication.timeSinceStartup - lastCleanupTime)).ToString("HH:mm:ss") : 
            "Never"));
        
        EditorGUILayout.EndScrollView();
        
        // Auto-repaint to update the memory display
        Repaint();
    }
    
    /// <summary>
    /// Draws the memory usage graph
    /// </summary>
    private void DrawMemoryGraph(Rect rect)
    {
        if (memoryHistory.Count < 2) return;
        
        // Draw background
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
        
        // Find min and max for scaling
        float maxValue = 0;
        foreach (float value in memoryHistory)
        {
            maxValue = Mathf.Max(maxValue, value);
        }
        
        maxValue = Mathf.Max(maxValue, memoryThresholdMB);
        maxValue *= 1.1f; // Add 10% headroom
        
        // Draw threshold line
        float thresholdY = rect.y + rect.height - (memoryThresholdMB / maxValue * rect.height);
        Handles.color = new Color(1f, 0.5f, 0, 0.8f); // Orange
        Handles.DrawLine(new Vector3(rect.x, thresholdY), new Vector3(rect.x + rect.width, thresholdY));
        
        // Draw memory line
        Handles.color = Color.green;
        for (int i = 0; i < memoryHistory.Count - 1; i++)
        {
            float startX = rect.x + (i / (float)(maxHistoryPoints - 1)) * rect.width;
            float endX = rect.x + ((i + 1) / (float)(maxHistoryPoints - 1)) * rect.width;
            
            float startY = rect.y + rect.height - (memoryHistory[i] / maxValue * rect.height);
            float endY = rect.y + rect.height - (memoryHistory[i + 1] / maxValue * rect.height);
            
            Handles.DrawLine(new Vector3(startX, startY), new Vector3(endX, endY));
        }
        
        // Draw labels
        EditorGUI.LabelField(new Rect(rect.x + 5, rect.y + 5, 100, 20), $"{maxValue:F0} MB");
        EditorGUI.LabelField(new Rect(rect.x + 5, rect.y + rect.height - 20, 100, 20), "0 MB");
        EditorGUI.LabelField(new Rect(rect.x + rect.width - 80, thresholdY - 15, 80, 20), $"Limit: {memoryThresholdMB:F0} MB");
    }
    
    /// <summary>
    /// Performs a memory cleanup operation
    /// </summary>
    public static void PerformCleanup()
    {
        UnityEngine.Debug.Log("[Redline Memory Master] Performing memory cleanup...");
        
        float beforeMB = GC.GetTotalMemory(false) / (1024f * 1024f);
        
        // Force a GC collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Try to unload unused assets
        EditorUtility.UnloadUnusedAssetsImmediate(true);
        
        // For more aggressive cleanup, you can also try:
        Resources.UnloadUnusedAssets();
        
        // Force another GC pass after asset unloading
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        float afterMB = GC.GetTotalMemory(false) / (1024f * 1024f);
        UnityEngine.Debug.Log($"[Redline Memory Master] Cleanup complete. Memory reduced from {beforeMB:F2}MB to {afterMB:F2}MB (saved {beforeMB - afterMB:F2}MB)");
        
        // Save settings after cleanup
        SaveSettings();
    }
    
    /// <summary>
    /// Validates and performs cleanup before opening build settings
    /// </summary>
    [MenuItem("File/Build Settings...", true, 2000)]
    public static bool BuildSettingsValidate()
    {
        PerformCleanup();
        return true;
    }
    
    /// <summary>
    /// Validates and performs cleanup before VRChat build and publish
    /// </summary>
    [MenuItem("VRChat SDK/Utilities/Build & Publish", true, 100)]
    public static bool VRChatBuildValidate()
    {
        UnityEngine.Debug.Log("[Redline Memory Master] VRChat Build & Publish detected, cleaning up memory...");
        PerformCleanup();
        return true;
    }
    
    /// <summary>
    /// Validates and performs cleanup before VRChat build
    /// </summary>
    [MenuItem("VRChat SDK/Utilities/Build", true, 100)]
    public static bool VRChatBuildOnlyValidate()
    {
        UnityEngine.Debug.Log("[Redline Memory Master] VRChat Build detected, cleaning up memory...");
        PerformCleanup();
        return true;
    }
    
    /// <summary>
    /// Generic validator for VRChat SDK menu items
    /// </summary>
    [MenuItem("VRChat SDK/", true, 100)]
    public static bool VRChatSDKGenericValidate()
    {
        PerformCleanup();
        return true;
    }
}