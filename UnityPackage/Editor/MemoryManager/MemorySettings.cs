using System;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.MemoryManager
{
    public class MemorySettings
    {
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

        public MemoryPreset SelectedPreset { get; set; } = MemoryPreset.AutoPhysical;
        public float MemoryThresholdMb { get; set; } = 2048;
        public bool AutoCleanupEnabled { get; set; }
        public float CleanupIntervalSeconds { get; set; } = 300;
        public bool ShowGraph { get; set; } = true;
        public float LastCleanupTime { get; set; }
        public float PeakMemoryUsageMb { get; set; }

        // GUI state
        public bool ShowSystemInfo { get; set; } = true;
        public bool ShowMemoryGraph { get; set; } = true;
        public bool ShowMemoryLimits { get; set; } = true;
        public bool ShowAutoCleanup { get; set; } = true;
        public bool ShowPoolStats { get; set; } = true;
        public bool ShowTextureManagement { get; set; } = true;
        public bool ShowGCCollection { get; set; } = true;
        public bool ShowAllocationPatterns { get; set; } = true;

        public void Load()
        {
            // Load saved preset
            var savedPreset = EditorPrefs.GetString(PrefSelectedPreset, MemoryPreset.AutoPhysical.ToString());
            if (Enum.TryParse<MemoryPreset>(savedPreset, out var preset))
            {
                SelectedPreset = preset;
            }

            // Load saved threshold
            MemoryThresholdMb = SelectedPreset == MemoryPreset.Custom 
                ? EditorPrefs.GetFloat(PrefCustomMemory, MemoryThresholdMb) 
                : GetPresetMemoryLimit(SelectedPreset);

            // Load auto cleanup settings
            AutoCleanupEnabled = EditorPrefs.GetBool(PrefAutoCleanup, false);
            CleanupIntervalSeconds = EditorPrefs.GetFloat(PrefCleanupInterval, 300f);

            // Load UI preferences
            ShowGraph = EditorPrefs.GetBool(PrefShowGraph, true);

            // Load last cleanup time and peak memory
            LastCleanupTime = EditorPrefs.GetFloat(PrefLastCleanupTime, 0f);
            PeakMemoryUsageMb = EditorPrefs.GetFloat(PrefPeakMemory, 0f);

            // Load GUI state
            ShowSystemInfo = EditorPrefs.GetBool(PrefShowSystemInfo, true);
            ShowMemoryGraph = EditorPrefs.GetBool(PrefShowMemoryGraph, true);
            ShowMemoryLimits = EditorPrefs.GetBool(PrefShowMemoryLimits, true);
            ShowAutoCleanup = EditorPrefs.GetBool(PrefShowAutoCleanup, true);
            ShowPoolStats = EditorPrefs.GetBool(PrefShowPoolStats, true);
            ShowTextureManagement = EditorPrefs.GetBool(PrefShowTextureManagement, true);
            ShowGCCollection = EditorPrefs.GetBool(PrefShowGCCollection, true);
            ShowAllocationPatterns = EditorPrefs.GetBool(PrefShowAllocationPatterns, true);
        }

        public void Save()
        {
            EditorPrefs.SetString(PrefSelectedPreset, SelectedPreset.ToString());
            if (SelectedPreset == MemoryPreset.Custom)
            {
                EditorPrefs.SetFloat(PrefCustomMemory, MemoryThresholdMb);
            }
            EditorPrefs.SetFloat(PrefMemoryThreshold, MemoryThresholdMb);
            EditorPrefs.SetBool(PrefAutoCleanup, AutoCleanupEnabled);
            EditorPrefs.SetFloat(PrefCleanupInterval, CleanupIntervalSeconds);
            EditorPrefs.SetBool(PrefShowGraph, ShowGraph);
            EditorPrefs.SetFloat(PrefLastCleanupTime, LastCleanupTime);
            EditorPrefs.SetFloat(PrefPeakMemory, PeakMemoryUsageMb);

            // Save GUI state
            EditorPrefs.SetBool(PrefShowSystemInfo, ShowSystemInfo);
            EditorPrefs.SetBool(PrefShowMemoryGraph, ShowMemoryGraph);
            EditorPrefs.SetBool(PrefShowMemoryLimits, ShowMemoryLimits);
            EditorPrefs.SetBool(PrefShowAutoCleanup, ShowAutoCleanup);
            EditorPrefs.SetBool(PrefShowPoolStats, ShowPoolStats);
            EditorPrefs.SetBool(PrefShowTextureManagement, ShowTextureManagement);
            EditorPrefs.SetBool(PrefShowGCCollection, ShowGCCollection);
            EditorPrefs.SetBool(PrefShowAllocationPatterns, ShowAllocationPatterns);
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
                _ => MemoryThresholdMb
            };
        }

        private float GetSystemMemory()
        {
            // This should be implemented to get actual system memory
            return 8192; // Default to 8GB
        }

        private float GetTotalSystemMemory()
        {
            // This should be implemented to get actual total system memory including swap
            return 16384; // Default to 16GB
        }
    }
} 