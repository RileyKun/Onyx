using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.MemoryManager
{
    public class MemoryUI
    {
        private Vector2 _scrollPosition;
        private readonly MemoryMonitor _monitor;
        private readonly MemorySettings _settings;
        private readonly MemoryCleaner _cleaner;
        private readonly MemoryLeakDetector _leakDetector;

        public MemoryUI()
        {
            _monitor = new MemoryMonitor();
            _settings = new MemorySettings();
            _cleaner = new MemoryCleaner();
            _leakDetector = new MemoryLeakDetector();
        }

        public void Draw(MemoryManagementTool window)
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            DrawSystemInfo();
            DrawMemoryUsage();
            DrawMemoryGraph();
            DrawMemoryLimits();
            DrawAutoCleanup();
            DrawPoolStats();
            DrawTextureManagement();
            DrawGCCollection();
            DrawAllocationPatterns();

            EditorGUILayout.EndScrollView();
            window.Repaint();
        }

        private void DrawHeader()
        {
            GUILayout.Label("Redline Memory Master", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        private void DrawSystemInfo()
        {
            _settings.ShowSystemInfo = EditorGUILayout.Foldout(_settings.ShowSystemInfo, "System Information", true);
            if (_settings.ShowSystemInfo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Physical RAM: {GetSystemMemory():F0} MB");
                EditorGUILayout.LabelField($"Swap/Pagefile: {GetSwapMemory():F0} MB");
                EditorGUILayout.LabelField($"System RAM + Swap: {GetTotalSystemMemory():F0} MB");
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMemoryUsage()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Current Unity Memory Usage: {_monitor.CurrentMemoryUsage:F2} MB");
            EditorGUILayout.LabelField($"Peak Memory Usage: {_monitor.PeakMemoryUsage:F2} MB");

            if (GUILayout.Button("Reset Peak"))
            {
                _monitor.ResetPeak();
                _settings.PeakMemoryUsageMb = _monitor.PeakMemoryUsage;
                _settings.Save();
            }

            var memoryPercentage = _monitor.CurrentMemoryUsage / _settings.MemoryThresholdMb;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20),
                Mathf.Clamp01(memoryPercentage),
                $"{_monitor.CurrentMemoryUsage:F0}/{_settings.MemoryThresholdMb:F0} MB ({(memoryPercentage * 100):F0}%)");

            if (GUILayout.Button("Force Memory Cleanup"))
            {
                _cleaner.PerformCleanup();
                _settings.LastCleanupTime = (float)EditorApplication.timeSinceStartup;
                _settings.Save();
            }
        }

        private void DrawMemoryGraph()
        {
            _settings.ShowMemoryGraph = EditorGUILayout.Foldout(_settings.ShowMemoryGraph, "Memory Usage Graph", true);
            if (_settings.ShowMemoryGraph && _monitor.MemoryHistory.Count > 0)
            {
                EditorGUI.indentLevel++;
                var graphRect = EditorGUILayout.GetControlRect(false, 100);
                DrawMemoryGraph(graphRect);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMemoryLimits()
        {
            _settings.ShowMemoryLimits = EditorGUILayout.Foldout(_settings.ShowMemoryLimits, "Memory Limit Settings", true);
            if (_settings.ShowMemoryLimits)
            {
                EditorGUI.indentLevel++;
                var presetNames = Enum.GetNames(typeof(MemoryPreset));
                var currentPresetIndex = Array.IndexOf(presetNames, _settings.SelectedPreset.ToString());
                var newPresetIndex = EditorGUILayout.Popup("Memory Preset", currentPresetIndex, presetNames);

                if (newPresetIndex != currentPresetIndex)
                {
                    _settings.SelectedPreset = Enum.Parse<MemoryPreset>(presetNames[newPresetIndex]);
                    if (_settings.SelectedPreset != MemoryPreset.Custom)
                    {
                        _settings.MemoryThresholdMb = GetPresetMemoryLimit(_settings.SelectedPreset);
                    }
                    _settings.Save();
                }

                if (_settings.SelectedPreset == MemoryPreset.Custom)
                {
                    float minMemory = 1024;
                    var maxMemory = Mathf.Max(GetTotalSystemMemory(), 16384);

                    EditorGUILayout.BeginHorizontal();
                    var newThreshold = EditorGUILayout.Slider(_settings.MemoryThresholdMb, minMemory, maxMemory);
                    var thresholdStr = EditorGUILayout.TextField(newThreshold.ToString("F0"), GUILayout.Width(60));
                    EditorGUILayout.LabelField("MB", GUILayout.Width(30));
                    EditorGUILayout.EndHorizontal();

                    if (float.TryParse(thresholdStr, out var parsedThreshold))
                    {
                        newThreshold = Mathf.Clamp(parsedThreshold, minMemory, maxMemory);
                    }

                    if (!Mathf.Approximately(newThreshold, _settings.MemoryThresholdMb))
                    {
                        _settings.MemoryThresholdMb = newThreshold;
                        _settings.Save();
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAutoCleanup()
        {
            _settings.ShowAutoCleanup = EditorGUILayout.Foldout(_settings.ShowAutoCleanup, "Auto Cleanup Settings", true);
            if (_settings.ShowAutoCleanup)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Memory threshold cleanup is always active. Timer-based cleanup can be enabled/disabled below.", MessageType.Info);

                var newAutoCleanup = EditorGUILayout.Toggle("Enable Timer-Based Cleanup", _settings.AutoCleanupEnabled);
                if (newAutoCleanup != _settings.AutoCleanupEnabled)
                {
                    _settings.AutoCleanupEnabled = newAutoCleanup;
                    _settings.Save();
                }

                var newInterval = EditorGUILayout.FloatField("Cleanup Interval (seconds)", _settings.CleanupIntervalSeconds);
                if (!Mathf.Approximately(newInterval, _settings.CleanupIntervalSeconds))
                {
                    _settings.CleanupIntervalSeconds = newInterval;
                    _settings.Save();
                }

                if (_settings.AutoCleanupEnabled)
                {
                    var timeUntilNextCleanup = _settings.CleanupIntervalSeconds - ((float)EditorApplication.timeSinceStartup - _settings.LastCleanupTime);
                    EditorGUILayout.LabelField(timeUntilNextCleanup > 0
                        ? $"Time until next timer-based cleanup: {timeUntilNextCleanup:F0} seconds"
                        : "Timer-based cleanup will run when interval is reached");
                }

                EditorGUILayout.LabelField("Last Cleanup: " + (_settings.LastCleanupTime > 0 ?
                    DateTime.Now.AddSeconds(-(EditorApplication.timeSinceStartup - _settings.LastCleanupTime)).ToString("HH:mm:ss") :
                    "Never"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawPoolStats()
        {
            _settings.ShowPoolStats = EditorGUILayout.Foldout(_settings.ShowPoolStats, "Object Pool Statistics", true);
            if (_settings.ShowPoolStats)
            {
                EditorGUI.indentLevel++;
                // Implementation moved to separate class
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTextureManagement()
        {
            _settings.ShowTextureManagement = EditorGUILayout.Foldout(_settings.ShowTextureManagement, "Texture Management", true);
            if (_settings.ShowTextureManagement)
            {
                EditorGUI.indentLevel++;
                // Implementation moved to separate class
                EditorGUI.indentLevel--;
            }
        }

        private void DrawGCCollection()
        {
            _settings.ShowGCCollection = EditorGUILayout.Foldout(_settings.ShowGCCollection, "GC Collection History", true);
            if (_settings.ShowGCCollection)
            {
                EditorGUI.indentLevel++;
                // Implementation moved to separate class
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAllocationPatterns()
        {
            _settings.ShowAllocationPatterns = EditorGUILayout.Foldout(_settings.ShowAllocationPatterns, "Memory Allocation Patterns", true);
            if (_settings.ShowAllocationPatterns)
            {
                EditorGUI.indentLevel++;
                // Implementation moved to separate class
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMemoryGraph(Rect rect)
        {
            if (_monitor.MemoryHistory.Count == 0) return;

            var maxMemory = _monitor.MemoryHistory.Max();
            var minMemory = _monitor.MemoryHistory.Min();
            var range = maxMemory - minMemory;
            if (range < 0.1f) range = 0.1f;

            var points = new Vector3[_monitor.MemoryHistory.Count];
            for (var i = 0; i < _monitor.MemoryHistory.Count; i++)
            {
                var x = rect.x + (rect.width * i / (_monitor.MemoryHistory.Count - 1));
                var y = rect.y + rect.height - ((_monitor.MemoryHistory[i] - minMemory) / range * rect.height);
                points[i] = new Vector3(x, y, 0);
            }

            Handles.color = Color.green;
            Handles.DrawAAPolyLine(2f, points);

            // Draw threshold line
            if (_settings.MemoryThresholdMb > minMemory && _settings.MemoryThresholdMb < maxMemory)
            {
                var thresholdY = rect.y + rect.height - ((_settings.MemoryThresholdMb - minMemory) / range * rect.height);
                Handles.color = Color.red;
                Handles.DrawLine(
                    new Vector3(rect.x, thresholdY, 0),
                    new Vector3(rect.x + rect.width, thresholdY, 0)
                );
            }
        }

        private float GetSystemMemory()
        {
            // Implementation moved to separate class
            return 8192; // Default to 8GB
        }

        private float GetSwapMemory()
        {
            // Implementation moved to separate class
            return 8192; // Default to 8GB
        }

        private float GetTotalSystemMemory()
        {
            return GetSystemMemory() + GetSwapMemory();
        }

        private float GetPresetMemoryLimit(MemoryPreset preset)
        {
            return preset switch
            {
                MemoryPreset.VeryLowEnd => 1024,
                MemoryPreset.LowEnd => 2048,
                MemoryPreset.Average => 4096,
                MemoryPreset.HighEnd => 8192,
                MemoryPreset.AutoPhysical => GetSystemMemory() / 2,
                MemoryPreset.AutoTotal => GetTotalSystemMemory() / 2,
                _ => _settings.MemoryThresholdMb
            };
        }
    }
} 