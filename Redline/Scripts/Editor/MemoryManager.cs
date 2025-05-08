using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Redline.Scripts.Editor
{
	/// <summary>
	/// Memory management tool for Unity Editor that monitors and controls memory usage.
	/// Provides automatic cleanup, memory threshold monitoring, and system memory detection.
	/// </summary>
	[InitializeOnLoad]
	public class MemoryManagementTool : EditorWindow
	{
		// Core memory tracking
		private static float _memoryUsageMb;
		private static float _peakMemoryUsageMb;
		private static float _lastCleanupTime;
		private static float _cleanupIntervalSeconds = 300; // 5 minutes between timer-based cleanups
		private static bool _autoCleanupEnabled;
		private static float _memoryThresholdMb = 2048;
		private static bool _initializedSystemMemory;
		private static bool _initializedSettings;

		// Memory limit presets
		// TODO: Why the hell are you using strings to index this array? This is slow.
		private static readonly Dictionary<string, float> MemoryPresets = new()
		{
			{"Very Low-End (1GB Limit)", 1024},
			{"Low-End (2GB Limit)", 2048},
			{"Average (4GB Limit)", 4096},
			{"High-End (8GB Limit)", 8192},
			{"Auto (50% of physical RAM)", 0},
			{"Auto (50% of RAM + Swap)", 0},
			{"Custom", 0}
		};
		private static string _selectedPreset = "Auto (50% of physical RAM)";
		private static Vector2 _scrollPosition;

		// EditorPrefs keys for settings persistence
		private const string PrefSelectedPreset = "Redline_MemoryManager_SelectedPreset";
		private const string PrefMemoryThreshold = "Redline_MemoryManager_Threshold";
		private const string PrefAutoCleanup = "Redline_MemoryManager_AutoCleanup";
		private const string PrefCleanupInterval = "Redline_MemoryManager_CleanupInterval";
		private const string PrefShowGraph = "Redline_MemoryManager_ShowGraph";
		private const string PrefLastCleanupTime = "Redline_MemoryManager_LastCleanupTime";
		private const string PrefPeakMemory = "Redline_MemoryManager_PeakMemory";
		private const string PrefCustomMemory = "Redline_MemoryManager_CustomMemory";

		// Memory usage graph data
		private static readonly List<float> MemoryHistory = new();
		private const int MaxHistoryPoints = 60;
		private static bool _showGraph = true;
		private const float GraphUpdateInterval = 1.0f;
		private static float _lastGraphUpdateTime;

		// System memory information
		private static float _totalSystemMemoryMb;
		private static float _totalPhysicalMemoryMb;
		private static float _totalSwapMb;

		// Windows API for physical memory detection
		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

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
			switch (_initializedSettings)
			{
				case false:
					LoadSettings();
					_initializedSettings = true;
					break;
			}

			if (_initializedSystemMemory) return;
			DetectSystemMemory();
			_initializedSystemMemory = true;
		}

		/// <summary>
		/// Loads all saved settings from EditorPrefs
		/// </summary>
		private static void LoadSettings()
		{
			// Load saved preset
			var savedPreset = EditorPrefs.GetString(PrefSelectedPreset, "Auto (50% of physical RAM)");
			if (MemoryPresets.ContainsKey(savedPreset))
			{
				_selectedPreset = savedPreset;
			}

			// Load saved threshold
			_memoryThresholdMb = _selectedPreset == "Custom" ? EditorPrefs.GetFloat(PrefCustomMemory, _memoryThresholdMb) : MemoryPresets[_selectedPreset];

			// Load auto cleanup settings
			_autoCleanupEnabled = EditorPrefs.GetBool(PrefAutoCleanup, false);
			_cleanupIntervalSeconds = EditorPrefs.GetFloat(PrefCleanupInterval, 300f);

			// Load UI preferences
			_showGraph = EditorPrefs.GetBool(PrefShowGraph, true);

			// Load last cleanup time and peak memory
			_lastCleanupTime = EditorPrefs.GetFloat(PrefLastCleanupTime, 0f);
			_peakMemoryUsageMb = EditorPrefs.GetFloat(PrefPeakMemory, 0f);
		}

		/// <summary>
		/// Saves all current settings to EditorPrefs
		/// </summary>
		private static void SaveSettings()
		{
			EditorPrefs.SetString(PrefSelectedPreset, _selectedPreset);
			if (_selectedPreset == "Custom")
			{
				EditorPrefs.SetFloat(PrefCustomMemory, _memoryThresholdMb);
			}
			EditorPrefs.SetFloat(PrefMemoryThreshold, _memoryThresholdMb);
			EditorPrefs.SetBool(PrefAutoCleanup, _autoCleanupEnabled);
			EditorPrefs.SetFloat(PrefCleanupInterval, _cleanupIntervalSeconds);
			EditorPrefs.SetBool(PrefShowGraph, _showGraph);
			EditorPrefs.SetFloat(PrefLastCleanupTime, _lastCleanupTime);
			EditorPrefs.SetFloat(PrefPeakMemory, _peakMemoryUsageMb);
		}

		/// <summary>
		/// Detects system memory information for the current platform
		/// </summary>
		private static void DetectSystemMemory()
		{
			try
			{
				_totalPhysicalMemoryMb = 0;
				_totalSwapMb = 0;
				_totalSystemMemoryMb = 0;

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
				if (_totalPhysicalMemoryMb < 1024)
				{
					UnityEngine.Debug.LogWarning("[Redline Memory Master] Total memory amount was found to be below 1024MiB... How are you even running this?");
					_totalPhysicalMemoryMb = _totalSystemMemoryMb = 1024;
					UnityEngine.Debug.LogWarning("[Redline Memory Master] Forcing memory amount to 1024.");
				}

				// Update the auto preset values
				MemoryPresets["Auto (50% of physical RAM)"] = _totalPhysicalMemoryMb / 2;
				MemoryPresets["Auto (50% of RAM + Swap)"] = _totalSystemMemoryMb / 2;

				_memoryThresholdMb = _selectedPreset switch
				{
					// Set the initial threshold based on the auto value
					"Auto (50% of physical RAM)" or "Auto (50% of RAM + Swap)" => MemoryPresets[_selectedPreset],
					_ => _memoryThresholdMb
				};

				UnityEngine.Debug.Log($"[Redline Memory Master] Detected physical RAM: {_totalPhysicalMemoryMb:F0}MB, swap: {_totalSwapMb:F0}MB, total: {_totalSystemMemoryMb:F0}MB");
				_initializedSystemMemory = true;
			}
			catch (Exception e)
			{
				UnityEngine.Debug.LogError($"[Redline Memory Master] Error detecting system memory: {e.Message}");

				// Fallback values
				_totalPhysicalMemoryMb = 8192;
				_totalSystemMemoryMb = _totalPhysicalMemoryMb;
				_totalSwapMb = 0;
				MemoryPresets["Auto (50% of physical RAM)"] = 4096;
				MemoryPresets["Auto (50% of RAM + Swap)"] = 4096;
				if (_selectedPreset is "Auto (50% of physical RAM)" or "Auto (50% of RAM + Swap)")
				{
					_memoryThresholdMb = 4096;
				}
				_initializedSystemMemory = true;
			}
		}

		/// <summary>
		/// Detects memory information on Windows systems
		/// </summary>
		private static void DetectWindowsMemory()
		{
			// Anyone ever heard of TotalPhysicalMemory or TotalVirtualMemory? This is a disgrace...
			if (!GetPhysicallyInstalledSystemMemory(out var memoryKb)) return;
			_totalPhysicalMemoryMb = memoryKb / 1024f;
			_totalSystemMemoryMb = _totalPhysicalMemoryMb;

			// Get page file info
			Process process = new();
			process.StartInfo.FileName = "wmic";
			process.StartInfo.Arguments = "pagefile list /format:list";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.CreateNoWindow = true;
			process.Start();

			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			if (!output.Contains("AllocatedBaseSize")) return;
			var index = output.IndexOf("AllocatedBaseSize=", StringComparison.Ordinal);
			if (index < 0) return;
			var sizeStr = output[(index + "AllocatedBaseSize=".Length)..];
			var endIndex = sizeStr.IndexOf('\r');
			if (endIndex < 0) return;
			sizeStr = sizeStr[..endIndex];
			if (!int.TryParse(sizeStr, out var pageFileMb)) return;
			_totalSwapMb = pageFileMb;
			_totalSystemMemoryMb += _totalSwapMb;
		}

		/// <summary>
		/// Detects memory information on Linux systems
		/// </summary>
		private static void DetectLinuxMemory()
		{
			Process process = new();
			process.StartInfo.FileName = "free";
			process.StartInfo.Arguments = "-m";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.CreateNoWindow = true;
			process.Start();

			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			var lines = output.Split('\n');
			switch (lines.Length)
			{
				case >= 2:
				{
					var memLine = lines[1];
					var parts = memLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length >= 2)
					{
						float.TryParse(parts[1], out _totalPhysicalMemoryMb);
						_totalSystemMemoryMb = _totalPhysicalMemoryMb;

						// Get swap information
						if (lines.Length >= 3)
						{
							var swapLine = lines[2];
							parts = swapLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
							if (parts.Length >= 2)
							{
								if (float.TryParse(parts[1], out var swapMb))
								{
									_totalSwapMb = swapMb;
									_totalSystemMemoryMb += _totalSwapMb;
								}
							}
						}
					}

					break;
				}
			}
		}

		/// <summary>
		/// Detects memory information on macOS systems
		/// </summary>
		private static void DetectMacOSMemory()
		{
			Process process = new();
			process.StartInfo.FileName = "sysctl";
			process.StartInfo.Arguments = "-n hw.memsize";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.CreateNoWindow = true;
			process.Start();

			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			switch (string.IsNullOrEmpty(output))
			{
				case false:
				{
					if (long.TryParse(output.Trim(), out var memoryBytes))
					{
						_totalPhysicalMemoryMb = memoryBytes / (1024f * 1024f);
						_totalSystemMemoryMb = _totalPhysicalMemoryMb;

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
							var startIdx = output.IndexOf("total = ", StringComparison.Ordinal) + "total = ".Length;
							var endIdx = output.IndexOf("M", startIdx, StringComparison.Ordinal);
							if (endIdx > startIdx)
							{
								var swapSizeStr = output.Substring(startIdx, endIdx - startIdx);
								if (float.TryParse(swapSizeStr, out var swapSizeMb))
								{
									_totalSwapMb = swapSizeMb;
									_totalSystemMemoryMb += _totalSwapMb;
								}
							}
						}
					}

					break;
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
			_memoryUsageMb = GC.GetTotalMemory(false) / (1024f * 1024f);

			// Update peak memory
			if (_memoryUsageMb > _peakMemoryUsageMb)
			{
				_peakMemoryUsageMb = _memoryUsageMb;
				SaveSettings();
			}

			// Update graph data
			if (EditorApplication.timeSinceStartup - _lastGraphUpdateTime > GraphUpdateInterval)
			{
				MemoryHistory.Add(_memoryUsageMb);
				if (MemoryHistory.Count > MaxHistoryPoints)
				{
					MemoryHistory.RemoveAt(0);
				}
				_lastGraphUpdateTime = (float)EditorApplication.timeSinceStartup;
			}

			// Check if we need to auto-cleanup based on memory threshold
			if (_memoryUsageMb > _memoryThresholdMb)
			{
				PerformCleanup();
				_lastCleanupTime = (float)EditorApplication.timeSinceStartup;
			}
			// Check if we need to auto-cleanup based on timer (if enabled)
			else if (_autoCleanupEnabled &&
			         (EditorApplication.timeSinceStartup - _lastCleanupTime > _cleanupIntervalSeconds))
			{
				PerformCleanup();
				_lastCleanupTime = (float)EditorApplication.timeSinceStartup;
			}
		}

		/// <summary>
		/// Draws the Memory Master window UI
		/// </summary>
		private void OnGUI()
		{
			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

			GUILayout.Label("Redline Memory Master", EditorStyles.boldLabel);

			// System memory info
			// TODO: Might be best to remove these if statements.
			if (_totalPhysicalMemoryMb > 0)
			{
				EditorGUILayout.LabelField($"Physical RAM: {_totalPhysicalMemoryMb:F0} MB");
			}
			if (_totalSwapMb > 0)
			{
				EditorGUILayout.LabelField($"Swap/Pagefile: {_totalSwapMb:F0} MB");
			}
			if (_totalSystemMemoryMb > 0)
			{
				EditorGUILayout.LabelField($"System RAM + Swap: {_totalSystemMemoryMb:F0} MB");
			}

			// Display current memory usage
			EditorGUILayout.LabelField($"Current Unity Memory Usage: {_memoryUsageMb:F2} MB");
			EditorGUILayout.LabelField($"Peak Memory Usage: {_peakMemoryUsageMb:F2} MB");

			// Reset peak button
			if (GUILayout.Button("Reset Peak"))
			{
				_peakMemoryUsageMb = _memoryUsageMb;
				SaveSettings();
			}

			// Progress bar for memory usage
			var memoryPercentage = _memoryUsageMb / _memoryThresholdMb;
			EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20),
				Mathf.Clamp01(memoryPercentage),
				$"{_memoryUsageMb:F0}/{_memoryThresholdMb:F0} MB ({(memoryPercentage * 100):F0}%)");

			// Manual cleanup button
			if (GUILayout.Button("Force Memory Cleanup"))
			{
				PerformCleanup();
				_lastCleanupTime = (float)EditorApplication.timeSinceStartup;
				SaveSettings();
			}

			EditorGUILayout.Space();

			// Memory graph
			var newShowGraph = EditorGUILayout.Foldout(_showGraph, "Memory Usage Graph", true);
			if (newShowGraph != _showGraph)
			{
				_showGraph = newShowGraph;
				SaveSettings();
			}
			if (_showGraph && MemoryHistory.Count > 0)
			{
				var graphRect = EditorGUILayout.GetControlRect(false, 100);
				DrawMemoryGraph(graphRect);
			}

			EditorGUILayout.Space();

			// Memory limit settings
			EditorGUILayout.LabelField("Memory Limit Settings", EditorStyles.boldLabel);

			// Preset selection
			var presetNames = new string[MemoryPresets.Count];
			MemoryPresets.Keys.CopyTo(presetNames, 0);

			var currentPresetIndex = Array.IndexOf(presetNames, _selectedPreset);
			var newPresetIndex = EditorGUILayout.Popup("Memory Preset", currentPresetIndex, presetNames);

			if (newPresetIndex != currentPresetIndex)
			{
				_selectedPreset = presetNames[newPresetIndex];
				if (_selectedPreset != "Custom")
				{
					_memoryThresholdMb = MemoryPresets[_selectedPreset];
				}
				SaveSettings();
			}

			// Custom memory input (only show when Custom is selected)
			if (_selectedPreset == "Custom")
			{
				float minMemory = 1024; // 1GB minimum
				var maxMemory = Mathf.Max(_totalSystemMemoryMb, 16384); // Use total system memory or 16GB as max

				EditorGUILayout.BeginHorizontal();
				var newThreshold = EditorGUILayout.Slider(_memoryThresholdMb, minMemory, maxMemory);
				var thresholdStr = EditorGUILayout.TextField(newThreshold.ToString("F0"), GUILayout.Width(60));
				EditorGUILayout.LabelField("MB", GUILayout.Width(30));
				EditorGUILayout.EndHorizontal();

				// Try to parse the text input
				if (float.TryParse(thresholdStr, out var parsedThreshold))
				{
					newThreshold = Mathf.Clamp(parsedThreshold, minMemory, maxMemory);
				}

				if (!Mathf.Approximately(newThreshold, _memoryThresholdMb))
				{
					_memoryThresholdMb = newThreshold;
					SaveSettings();
				}
			}

			// Auto cleanup settings
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Auto Cleanup Settings", EditorStyles.boldLabel);

			EditorGUILayout.HelpBox("Memory threshold cleanup is always active. Timer-based cleanup can be enabled/disabled below.", MessageType.Info);

			var newAutoCleanup = EditorGUILayout.Toggle("Enable Timer-Based Cleanup", _autoCleanupEnabled);
			if (newAutoCleanup != _autoCleanupEnabled)
			{
				_autoCleanupEnabled = newAutoCleanup;
				SaveSettings();
			}

			var newInterval = EditorGUILayout.FloatField("Cleanup Interval (seconds)", _cleanupIntervalSeconds);
			if (!Mathf.Approximately(newInterval, _cleanupIntervalSeconds))
			{
				_cleanupIntervalSeconds = newInterval;
				SaveSettings();
			}

			// Display time until next cleanup
			if (_autoCleanupEnabled)
			{
				var timeUntilNextCleanup = _cleanupIntervalSeconds - ((float)EditorApplication.timeSinceStartup - _lastCleanupTime);
				EditorGUILayout.LabelField(timeUntilNextCleanup > 0
					? $"Time until next timer-based cleanup: {timeUntilNextCleanup:F0} seconds"
					: "Timer-based cleanup will run when interval is reached");
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Last Cleanup: " + (_lastCleanupTime > 0 ?
				DateTime.Now.AddSeconds(-(EditorApplication.timeSinceStartup - _lastCleanupTime)).ToString("HH:mm:ss") :
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
			if (MemoryHistory.Count < 2) return;

			// Draw background
			EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

			// Find min and max for scaling
			float maxValue = 0;
			foreach (var value in MemoryHistory)
			{
				maxValue = Mathf.Max(maxValue, value);
			}

			maxValue = Mathf.Max(maxValue, _memoryThresholdMb);
			maxValue *= 1.1f; // Add 10% headroom

			// Draw threshold line
			var thresholdY = rect.y + rect.height - (_memoryThresholdMb / maxValue * rect.height);
			Handles.color = new Color(1f, 0.5f, 0, 0.8f); // Orange
			Handles.DrawLine(new Vector3(rect.x, thresholdY), new Vector3(rect.x + rect.width, thresholdY));

			// Draw memory line
			Handles.color = Color.green;
			for (var i = 0; i < MemoryHistory.Count - 1; i++)
			{
				var startX = rect.x + (i / (float)(MaxHistoryPoints - 1)) * rect.width;
				var endX = rect.x + ((i + 1) / (float)(MaxHistoryPoints - 1)) * rect.width;

				var startY = rect.y + rect.height - (MemoryHistory[i] / maxValue * rect.height);
				var endY = rect.y + rect.height - (MemoryHistory[i + 1] / maxValue * rect.height);

				Handles.DrawLine(new Vector3(startX, startY), new Vector3(endX, endY));
			}

			// Draw labels
			EditorGUI.LabelField(new Rect(rect.x + 5, rect.y + 5, 100, 20), $"{maxValue:F0} MB");
			EditorGUI.LabelField(new Rect(rect.x + 5, rect.y + rect.height - 20, 100, 20), "0 MB");
			EditorGUI.LabelField(new Rect(rect.x + rect.width - 80, thresholdY - 15, 80, 20), $"Limit: {_memoryThresholdMb:F0} MB");
		}

		/// <summary>
		/// Performs a memory cleanup operation
		/// </summary>
		public static void PerformCleanup()
		{
			UnityEngine.Debug.Log("[Redline Memory Master] Performing memory cleanup...");

			var beforeMb = GC.GetTotalMemory(false) / (1024f * 1024f);

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

			var afterMb = GC.GetTotalMemory(false) / (1024f * 1024f);
			UnityEngine.Debug.Log($"[Redline Memory Master] Cleanup complete. Memory reduced from {beforeMb:F2}MB to {afterMb:F2}MB (saved {beforeMb - afterMb:F2}MB)");

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
}
