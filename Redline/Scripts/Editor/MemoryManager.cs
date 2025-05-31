using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using System.Linq;

namespace Redline.Scripts.Editor
{
	/// <summary>
	/// Memory management tool for Unity Editor that monitors and controls memory usage.
	/// Provides automatic cleanup, memory threshold monitoring, and system memory detection.
	/// </summary>
	[InitializeOnLoad]
	public class MemoryManagementTool : EditorWindow
	{
		// Memory pooling system
		private static readonly Dictionary<Type, Queue<object>> _objectPools = new();
		private static readonly Dictionary<Type, int> _poolSizes = new();
		private const int DefaultPoolSize = 100;

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

		// GUI state persistence keys
		private const string PrefShowSystemInfo = "Redline_MemoryManager_ShowSystemInfo";
		private const string PrefShowMemoryGraph = "Redline_MemoryManager_ShowMemoryGraph";
		private const string PrefShowMemoryLimits = "Redline_MemoryManager_ShowMemoryLimits";
		private const string PrefShowAutoCleanup = "Redline_MemoryManager_ShowAutoCleanup";
		private const string PrefShowPoolStats = "Redline_MemoryManager_ShowPoolStats";
		private const string PrefShowTextureManagement = "Redline_MemoryManager_ShowTextureManagement";
		private const string PrefShowGCCollection = "Redline_MemoryManager_ShowGCCollection";
		private const string PrefShowAllocationPatterns = "Redline_MemoryManager_ShowAllocationPatterns";

		// GUI state
		private static bool _showSystemInfo = true;
		private static bool _showMemoryGraph = true;
		private static bool _showMemoryLimits = true;
		private static bool _showAutoCleanup = true;
		private static bool _showPoolStats = true;
		private static bool _showTextureManagement = true;
		private static bool _showGCCollection = true;
		private static bool _showAllocationPatterns = true;

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

		// Texture memory management
		private static readonly Dictionary<Texture2D, TextureMemoryInfo> _textureMemoryInfo = new();
		private static float _totalTextureMemoryMb;
		private const float TextureMemoryWarningThreshold = 0.8f; // 80% of total memory

		// Asset bundle memory management
		private static readonly Dictionary<string, AssetBundle> _loadedAssetBundles = new();
		private static readonly Dictionary<string, DateTime> _assetBundleLastAccess = new();
		private const float AssetBundleUnloadTimeMinutes = 30f; // Unload unused asset bundles after 30 minutes

		// Memory leak detection
		private static readonly Dictionary<Type, int> _objectCounts = new();
		private static readonly Dictionary<Type, float> _objectMemoryUsage = new();
		private static readonly Queue<MemorySnapshot> _memorySnapshots = new();
		private const int MaxSnapshots = 10;
		private const float MemoryLeakThreshold = 0.2f; // 20% increase in memory usage

		private static readonly Dictionary<Type, PoolStatistics> _poolStatistics = new();
		private static readonly List<GCCollectionInfo> _gcCollectionHistory = new();
		private static readonly List<MemoryAllocationInfo> _memoryAllocationHistory = new();

		private class TextureMemoryInfo
		{
			public float MemorySizeMb;
			public bool IsCompressed;
			public int Width;
			public int Height;
			public TextureFormat Format;
			public DateTime LastAccess;
		}

		private class MemorySnapshot
		{
			public DateTime Timestamp;
			public Dictionary<Type, int> ObjectCounts;
			public Dictionary<Type, float> ObjectMemoryUsage;
			public float TotalMemoryUsage;
		}

		private class PoolStatistics
		{
			public int CurrentSize;
			public int PeakSize;
			public int TotalCreated;
			public int TotalReused;
		}

		private class GCCollectionInfo
		{
			public DateTime Timestamp;
			public long MemoryBefore;
			public long MemoryAfter;
			public int CollectionCount;
		}

		private class MemoryAllocationInfo
		{
			public Type ObjectType;
			public long AllocationSize;
			public DateTime Timestamp;
		}

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

			// Load GUI state
			_showSystemInfo = EditorPrefs.GetBool(PrefShowSystemInfo, true);
			_showMemoryGraph = EditorPrefs.GetBool(PrefShowMemoryGraph, true);
			_showMemoryLimits = EditorPrefs.GetBool(PrefShowMemoryLimits, true);
			_showAutoCleanup = EditorPrefs.GetBool(PrefShowAutoCleanup, true);
			_showPoolStats = EditorPrefs.GetBool(PrefShowPoolStats, true);
			_showTextureManagement = EditorPrefs.GetBool(PrefShowTextureManagement, true);
			_showGCCollection = EditorPrefs.GetBool(PrefShowGCCollection, true);
			_showAllocationPatterns = EditorPrefs.GetBool(PrefShowAllocationPatterns, true);
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

			// Save GUI state
			EditorPrefs.SetBool(PrefShowSystemInfo, _showSystemInfo);
			EditorPrefs.SetBool(PrefShowMemoryGraph, _showMemoryGraph);
			EditorPrefs.SetBool(PrefShowMemoryLimits, _showMemoryLimits);
			EditorPrefs.SetBool(PrefShowAutoCleanup, _showAutoCleanup);
			EditorPrefs.SetBool(PrefShowPoolStats, _showPoolStats);
			EditorPrefs.SetBool(PrefShowTextureManagement, _showTextureManagement);
			EditorPrefs.SetBool(PrefShowGCCollection, _showGCCollection);
			EditorPrefs.SetBool(PrefShowAllocationPatterns, _showAllocationPatterns);
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
			// Anyone ever heard of TotalPhysicallyInstalledSystemMemory or TotalVirtualMemory? This is a disgrace...
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

				// Take memory snapshot for leak detection
				TakeMemorySnapshot();
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

			// System Info Section
			_showSystemInfo = EditorGUILayout.Foldout(_showSystemInfo, "System Information", true);
			if (_showSystemInfo)
			{
				EditorGUI.indentLevel++;
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
				EditorGUI.indentLevel--;
			}

			// Current Memory Usage Section
			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Current Unity Memory Usage: {_memoryUsageMb:F2} MB");
			EditorGUILayout.LabelField($"Peak Memory Usage: {_peakMemoryUsageMb:F2} MB");

			if (GUILayout.Button("Reset Peak"))
			{
				_peakMemoryUsageMb = _memoryUsageMb;
				SaveSettings();
			}

			// Memory Usage Progress Bar
			var memoryPercentage = _memoryUsageMb / _memoryThresholdMb;
			EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20),
				Mathf.Clamp01(memoryPercentage),
				$"{_memoryUsageMb:F0}/{_memoryThresholdMb:F0} MB ({(memoryPercentage * 100):F0}%)");

			if (GUILayout.Button("Force Memory Cleanup"))
			{
				PerformCleanup();
				_lastCleanupTime = (float)EditorApplication.timeSinceStartup;
				SaveSettings();
			}

			// Memory Graph Section
			_showMemoryGraph = EditorGUILayout.Foldout(_showMemoryGraph, "Memory Usage Graph", true);
			if (_showMemoryGraph && MemoryHistory.Count > 0)
			{
				EditorGUI.indentLevel++;
				var graphRect = EditorGUILayout.GetControlRect(false, 100);
				DrawMemoryGraph(graphRect);
				EditorGUI.indentLevel--;
			}

			// Memory Limits Section
			_showMemoryLimits = EditorGUILayout.Foldout(_showMemoryLimits, "Memory Limit Settings", true);
			if (_showMemoryLimits)
			{
				EditorGUI.indentLevel++;
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

				if (_selectedPreset == "Custom")
				{
					float minMemory = 1024;
					var maxMemory = Mathf.Max(_totalSystemMemoryMb, 16384);

					EditorGUILayout.BeginHorizontal();
					var newThreshold = EditorGUILayout.Slider(_memoryThresholdMb, minMemory, maxMemory);
					var thresholdStr = EditorGUILayout.TextField(newThreshold.ToString("F0"), GUILayout.Width(60));
					EditorGUILayout.LabelField("MB", GUILayout.Width(30));
					EditorGUILayout.EndHorizontal();

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
				EditorGUI.indentLevel--;
			}

			// Auto Cleanup Section
			_showAutoCleanup = EditorGUILayout.Foldout(_showAutoCleanup, "Auto Cleanup Settings", true);
			if (_showAutoCleanup)
			{
				EditorGUI.indentLevel++;
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

				if (_autoCleanupEnabled)
				{
					var timeUntilNextCleanup = _cleanupIntervalSeconds - ((float)EditorApplication.timeSinceStartup - _lastCleanupTime);
					EditorGUILayout.LabelField(timeUntilNextCleanup > 0
						? $"Time until next timer-based cleanup: {timeUntilNextCleanup:F0} seconds"
						: "Timer-based cleanup will run when interval is reached");
				}

				EditorGUILayout.LabelField("Last Cleanup: " + (_lastCleanupTime > 0 ?
					DateTime.Now.AddSeconds(-(EditorApplication.timeSinceStartup - _lastCleanupTime)).ToString("HH:mm:ss") :
					"Never"));
				EditorGUI.indentLevel--;
			}

			// Pool Statistics Section
			_showPoolStats = EditorGUILayout.Foldout(_showPoolStats, "Object Pool Statistics", true);
			if (_showPoolStats)
			{
				EditorGUI.indentLevel++;
				foreach (var kvp in _poolStatistics)
				{
					var type = kvp.Key;
					var stats = kvp.Value;
					var reuseRate = stats.TotalCreated > 0 ? (float)stats.TotalReused / stats.TotalCreated : 0f;

					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					EditorGUILayout.LabelField(type.Name, EditorStyles.boldLabel);
					EditorGUILayout.LabelField($"Current Size: {stats.CurrentSize}");
					EditorGUILayout.LabelField($"Peak Size: {stats.PeakSize}");
					EditorGUILayout.LabelField($"Reuse Rate: {reuseRate:P2}");
					EditorGUILayout.LabelField($"Total Created: {stats.TotalCreated}");
					EditorGUILayout.LabelField($"Total Reused: {stats.TotalReused}");
					EditorGUILayout.EndVertical();
				}
				EditorGUI.indentLevel--;
			}

			// Texture Management Section
			_showTextureManagement = EditorGUILayout.Foldout(_showTextureManagement, "Texture Management", true);
			if (_showTextureManagement)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.LabelField($"Total Texture Memory: {_totalTextureMemoryMb:F2} MB");
				EditorGUILayout.LabelField($"Texture Memory Limit: {_memoryThresholdMb * TextureMemoryWarningThreshold:F2} MB");

				var textureMemoryPercentage = _totalTextureMemoryMb / (_memoryThresholdMb * TextureMemoryWarningThreshold);
				EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20),
					Mathf.Clamp01(textureMemoryPercentage),
					$"{_totalTextureMemoryMb:F0}/{_memoryThresholdMb * TextureMemoryWarningThreshold:F0} MB ({(textureMemoryPercentage * 100):F0}%)");

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Texture Optimization Settings", EditorStyles.boldLabel);

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Enable Automatic Texture Optimization", GUILayout.Width(250));
				var textureOptimizationEnabled = EditorGUILayout.Toggle(true);
				EditorGUILayout.EndHorizontal();

				if (textureOptimizationEnabled)
				{
					EditorGUILayout.HelpBox("Large textures will be automatically compressed and resized when memory usage is high.", MessageType.Info);
				}

				var largeTextures = _textureMemoryInfo
					.Where(kvp => kvp.Value.MemorySizeMb > 1f)
					.OrderByDescending(kvp => kvp.Value.MemorySizeMb)
					.Take(5);

				if (largeTextures.Any())
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Large Textures", EditorStyles.boldLabel);
					foreach (var kvp in largeTextures)
					{
						var texture = kvp.Key;
						var info = kvp.Value;
						EditorGUILayout.BeginVertical(EditorStyles.helpBox);
						EditorGUILayout.LabelField(texture.name, EditorStyles.boldLabel);
						EditorGUILayout.LabelField($"Size: {info.Width}x{info.Height}");
						EditorGUILayout.LabelField($"Memory: {info.MemorySizeMb:F2} MB");
						EditorGUILayout.LabelField($"Format: {info.Format}");
						EditorGUILayout.LabelField($"Compressed: {info.IsCompressed}");
						EditorGUILayout.EndVertical();
					}
				}
				EditorGUI.indentLevel--;
			}

			// GC Collection History Section
			_showGCCollection = EditorGUILayout.Foldout(_showGCCollection, "GC Collection History", true);
			if (_showGCCollection)
			{
				EditorGUI.indentLevel++;
				if (_gcCollectionHistory.Count > 0)
				{
					var lastCollection = _gcCollectionHistory[_gcCollectionHistory.Count - 1];
					EditorGUILayout.LabelField($"Last Collection: {lastCollection.Timestamp:HH:mm:ss}");
					EditorGUILayout.LabelField($"Memory Before: {lastCollection.MemoryBefore / (1024f * 1024f):F2} MB");
					EditorGUILayout.LabelField($"Memory After: {lastCollection.MemoryAfter / (1024f * 1024f):F2} MB");
					EditorGUILayout.LabelField($"Collection Count: {lastCollection.CollectionCount}");

					var gcGraphRect = EditorGUILayout.GetControlRect(false, 100);
					DrawGCCollectionGraph(gcGraphRect);
				}
				EditorGUI.indentLevel--;
			}

			// Memory Allocation Patterns Section
			_showAllocationPatterns = EditorGUILayout.Foldout(_showAllocationPatterns, "Memory Allocation Patterns", true);
			if (_showAllocationPatterns)
			{
				EditorGUI.indentLevel++;
				var typeAllocations = new Dictionary<Type, long>();
				foreach (var info in _memoryAllocationHistory)
				{
					if (info.ObjectType != null)
					{
						if (!typeAllocations.ContainsKey(info.ObjectType))
						{
							typeAllocations[info.ObjectType] = 0;
						}
						typeAllocations[info.ObjectType] += info.AllocationSize;
					}
				}

				var topAllocations = typeAllocations
					.OrderByDescending(kvp => kvp.Value)
					.Take(5);

				if (topAllocations.Any())
				{
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					foreach (var (type, size) in topAllocations)
					{
						EditorGUILayout.BeginHorizontal();
						EditorGUILayout.LabelField(type.Name, GUILayout.Width(200));
						EditorGUILayout.LabelField($"{size / (1024f * 1024f):F2} MB", GUILayout.Width(100));
						EditorGUILayout.EndHorizontal();
					}
					EditorGUILayout.EndVertical();
				}
				else
				{
					EditorGUILayout.HelpBox("No memory allocation data available yet. This will populate as objects are created.", MessageType.Info);
				}
				EditorGUI.indentLevel--;
			}

			// Add some padding at the bottom
			EditorGUILayout.Space(20);

			EditorGUILayout.EndScrollView();

			// Auto-repaint to update the memory display
			Repaint();
		}

		/// <summary>
		/// Unloads unused asset bundles
		/// </summary>
		private static void UnloadUnusedAssetBundles()
		{
			var bundlesToUnload = _assetBundleLastAccess
				.Where(kvp => (DateTime.Now - kvp.Value).TotalMinutes > AssetBundleUnloadTimeMinutes)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var bundleName in bundlesToUnload)
			{
				if (_loadedAssetBundles.TryGetValue(bundleName, out var bundle))
				{
					if (bundle != null)
					{
						bundle.Unload(true);
						UnityEngine.Debug.Log($"[Redline Memory Master] Unloaded unused asset bundle: {bundleName}");
					}
					_loadedAssetBundles.Remove(bundleName);
					_assetBundleLastAccess.Remove(bundleName);
				}
			}
		}

		/// <summary>
		/// Performs a memory cleanup operation
		/// </summary>
		public static void PerformCleanup()
		{
			UnityEngine.Debug.Log("[Redline Memory Master] Performing memory cleanup...");

			var beforeMb = GC.GetTotalMemory(false) / (1024f * 1024f);

			// Clear object pools first
			ClearPools();

			// Optimize textures
			OptimizeTextures();

			// Unload unused asset bundles
			UnloadUnusedAssetBundles();

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

		/// <summary>
		/// Takes a memory snapshot for leak detection
		/// </summary>
		private static void TakeMemorySnapshot()
		{
			var snapshot = new MemorySnapshot
			{
				Timestamp = DateTime.Now,
				ObjectCounts = new Dictionary<Type, int>(_objectCounts),
				ObjectMemoryUsage = new Dictionary<Type, float>(_objectMemoryUsage),
				TotalMemoryUsage = _memoryUsageMb
			};

			_memorySnapshots.Enqueue(snapshot);
			if (_memorySnapshots.Count > MaxSnapshots)
			{
				_memorySnapshots.Dequeue();
			}

			// Check for potential memory leaks
			if (_memorySnapshots.Count >= 2)
			{
				var oldestSnapshot = _memorySnapshots.Peek();
				var memoryIncrease = (_memoryUsageMb - oldestSnapshot.TotalMemoryUsage) / oldestSnapshot.TotalMemoryUsage;

				if (memoryIncrease > MemoryLeakThreshold)
				{
					UnityEngine.Debug.LogWarning($"[Redline Memory Master] Potential memory leak detected! Memory usage increased by {(memoryIncrease * 100):F1}% over {MaxSnapshots} snapshots");
					AnalyzeMemoryLeak(oldestSnapshot);
				}
			}
		}

		/// <summary>
		/// Analyzes potential memory leaks by comparing object counts and memory usage
		/// </summary>
		private static void AnalyzeMemoryLeak(MemorySnapshot oldestSnapshot)
		{
			var suspiciousTypes = new List<(Type type, float increase)>();

			foreach (var kvp in _objectCounts)
			{
				if (oldestSnapshot.ObjectCounts.TryGetValue(kvp.Key, out var oldCount))
				{
					var countIncrease = (float)(kvp.Value - oldCount) / oldCount;
					if (countIncrease > MemoryLeakThreshold)
					{
						suspiciousTypes.Add((kvp.Key, countIncrease));
					}
				}
			}

			if (suspiciousTypes.Any())
			{
				UnityEngine.Debug.LogWarning("[Redline Memory Master] Suspicious object type increases:");
				foreach (var (type, increase) in suspiciousTypes.OrderByDescending(x => x.increase))
				{
					UnityEngine.Debug.LogWarning($"- {type.Name}: {increase * 100:F1}% increase in count");
				}
			}
		}

		/// <summary>
		/// Draws the memory usage graph in the editor window
		/// </summary>
		private void DrawMemoryGraph(Rect rect)
		{
			if (MemoryHistory.Count == 0) return;

			var maxMemory = MemoryHistory.Max();
			var minMemory = MemoryHistory.Min();
			var range = maxMemory - minMemory;
			if (range < 0.1f) range = 0.1f;

			var points = new Vector3[MemoryHistory.Count];
			for (var i = 0; i < MemoryHistory.Count; i++)
			{
				var x = rect.x + (rect.width * i / (MemoryHistory.Count - 1));
				var y = rect.y + rect.height - ((MemoryHistory[i] - minMemory) / range * rect.height);
				points[i] = new Vector3(x, y, 0);
			}

			Handles.color = Color.green;
			Handles.DrawAAPolyLine(2f, points);

			// Draw threshold line
			if (_memoryThresholdMb > minMemory && _memoryThresholdMb < maxMemory)
			{
				var thresholdY = rect.y + rect.height - ((_memoryThresholdMb - minMemory) / range * rect.height);
				Handles.color = Color.red;
				Handles.DrawLine(
					new Vector3(rect.x, thresholdY, 0),
					new Vector3(rect.x + rect.width, thresholdY, 0)
				);
			}
		}

		/// <summary>
		/// Draws the GC collection history graph
		/// </summary>
		private void DrawGCCollectionGraph(Rect rect)
		{
			if (_gcCollectionHistory.Count == 0) return;

			var maxMemory = (float)_gcCollectionHistory.Max(x => x.MemoryBefore);
			var minMemory = (float)_gcCollectionHistory.Min(x => x.MemoryAfter);
			var range = maxMemory - minMemory;
			if (range < 0.1f) range = 0.1f;

			var points = new Vector3[_gcCollectionHistory.Count * 2];
			for (var i = 0; i < _gcCollectionHistory.Count; i++)
			{
				var collection = _gcCollectionHistory[i];
				var x = rect.x + (rect.width * i / (_gcCollectionHistory.Count - 1));

				// Before collection point
				var beforeY = rect.y + rect.height - (((float)collection.MemoryBefore - minMemory) / range * rect.height);
				points[i * 2] = new Vector3(x, beforeY, 0);

				// After collection point
				var afterY = rect.y + rect.height - (((float)collection.MemoryAfter - minMemory) / range * rect.height);
				points[i * 2 + 1] = new Vector3(x, afterY, 0);
			}

			Handles.color = Color.yellow;
			Handles.DrawAAPolyLine(2f, points);
		}

		/// <summary>
		/// Clears all object pools
		/// </summary>
		private static void ClearPools()
		{
			foreach (var pool in _objectPools.Values)
			{
				pool.Clear();
			}
			_objectPools.Clear();
			_poolSizes.Clear();
		}

		/// <summary>
		/// Optimizes textures to reduce memory usage
		/// </summary>
		private static void OptimizeTextures()
		{
			var texturesToOptimize = _textureMemoryInfo
				.Where(kvp => kvp.Value.MemorySizeMb > 1f && !kvp.Value.IsCompressed)
				.OrderByDescending(kvp => kvp.Value.MemorySizeMb)
				.Take(5);

			foreach (var kvp in texturesToOptimize)
			{
				var texture = kvp.Key;
				var info = kvp.Value;

				// Skip if texture is null or already destroyed
				if (texture == null) continue;

				// Create a temporary copy of the texture
				var tempTexture = new Texture2D(texture.width, texture.height, texture.format, texture.mipmapCount > 1);
				tempTexture.SetPixels(texture.GetPixels());
				tempTexture.Apply();

				// Compress the texture
				var compressedTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, texture.mipmapCount > 1);
				compressedTexture.SetPixels(tempTexture.GetPixels());
				compressedTexture.Apply();

				// Replace the original texture
				EditorUtility.CopySerialized(compressedTexture, texture);

				// Clean up temporary textures
				DestroyImmediate(tempTexture);
				DestroyImmediate(compressedTexture);

				// Update texture info
				info.IsCompressed = true;
				info.MemorySizeMb = CalculateTextureMemorySize(texture);
			}
		}

		/// <summary>
		/// Calculates the memory size of a texture in megabytes
		/// </summary>
		private static float CalculateTextureMemorySize(Texture2D texture)
		{
			if (texture == null) return 0f;

			var width = texture.width;
			var height = texture.height;
			var format = texture.format;
			var mipmapCount = texture.mipmapCount;

			// Calculate base size
			long bytesPerPixel = format switch
			{
				TextureFormat.RGBA32 => 4L,
				TextureFormat.RGB24 => 3L,
				TextureFormat.RGBA64 => 8L,
				TextureFormat.RGBAFloat => 16L,
				_ => 4L // Default to RGBA32
			};

			var baseSize = width * height * bytesPerPixel;

			// Calculate mipmap size
			var mipmapSize = 0L;
			if (mipmapCount > 1)
			{
				var currentWidth = width;
				var currentHeight = height;
				for (var i = 1; i < mipmapCount; i++)
				{
					currentWidth /= 2;
					currentHeight /= 2;
					mipmapSize += currentWidth * currentHeight * bytesPerPixel;
				}
			}

			return (baseSize + mipmapSize) / (1024f * 1024f);
		}
	}
}
