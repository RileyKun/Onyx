using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Redline.Editor.MemoryManager
{
    /// <summary>
    /// Interface for memory monitoring functionality
    /// </summary>
    public interface IMemoryMonitor
    {
        float CurrentMemoryUsage { get; }
        float PeakMemoryUsage { get; }
        List<float> MemoryHistory { get; }
        void Update();
        void ResetPeak();
        float GetAverageMemoryUsage();
        float GetMemoryUsagePercentile(float percentile);
    }

    /// <summary>
    /// Interface for memory cleaning functionality
    /// </summary>
    public interface IMemoryCleaner
    {
        void PerformCleanup();
        void OptimizeTextures();
        void ClearPools();
    }

    /// <summary>
    /// Interface for memory leak detection functionality
    /// </summary>
    public interface IMemoryLeakDetector
    {
        bool HasLeaks { get; }
        void TakeSnapshot();
        void AnalyzeLeaks();
    }

    /// <summary>
    /// Enum defining memory limit presets
    /// </summary>
    public enum MemoryPreset
    {
        [Description("1GB - Minimum (Very Low-End PC)")]
        VeryLowEnd = 0,

        [Description("2GB - Low-End PC")]
        LowEnd = 1,

        [Description("4GB - Standard PC")]
        Average = 2,

        [Description("8GB - High-End PC")]
        HighEnd = 3,

        [Description("Auto (Half of Physical RAM)")]
        AutoPhysical = 4,

        [Description("Auto (Half of Total Memory)")]
        AutoTotal = 5,

        [Description("Custom Memory Limit")]
        Custom = 6
    }

    public static class MemoryPresetExtensions
    {
        public static string GetDescription(this MemoryPreset preset)
        {
            var field = preset.GetType().GetField(preset.ToString());
            var attribute = (DescriptionAttribute)field.GetCustomAttributes(typeof(DescriptionAttribute), false)[0];
            return attribute.Description;
        }

        public static MemoryPreset FromDescription(string description)
        {
            foreach (MemoryPreset preset in System.Enum.GetValues(typeof(MemoryPreset)))
            {
                if (preset.GetDescription() == description)
                    return preset;
            }
            return MemoryPreset.AutoPhysical; // Default to AutoPhysical if not found
        }
    }
} 