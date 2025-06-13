using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Redline.Editor.MemoryManager
{
    public class MemoryMonitor : IMemoryMonitor
    {
        private readonly List<float> _memoryHistory = new();
        private const int MaxHistoryPoints = 60;
        private float _peakMemoryUsage;
        private float _currentMemoryUsage;

        public float CurrentMemoryUsage => _currentMemoryUsage;
        public float PeakMemoryUsage => _peakMemoryUsage;
        public List<float> MemoryHistory => _memoryHistory;

        public void Update()
        {
            _currentMemoryUsage = GC.GetTotalMemory(false) / (1024f * 1024f);
            
            if (_currentMemoryUsage > _peakMemoryUsage)
            {
                _peakMemoryUsage = _currentMemoryUsage;
            }

            UpdateMemoryHistory();
        }

        public void ResetPeak()
        {
            _peakMemoryUsage = _currentMemoryUsage;
        }

        public float GetAverageMemoryUsage()
        {
            if (_memoryHistory.Count == 0) return 0f;
            return _memoryHistory.Average();
        }

        public float GetMemoryUsagePercentile(float percentile)
        {
            if (_memoryHistory.Count == 0) return 0f;
            
            var sortedHistory = _memoryHistory.OrderBy(x => x).ToList();
            var index = (int)Math.Ceiling(percentile / 100f * sortedHistory.Count) - 1;
            return sortedHistory[index];
        }

        private void UpdateMemoryHistory()
        {
            _memoryHistory.Add(_currentMemoryUsage);
            if (_memoryHistory.Count > MaxHistoryPoints)
            {
                _memoryHistory.RemoveAt(0);
            }
        }
    }
} 