using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Redline.Editor.MemoryManager
{
    public class MemoryLeakDetector : IMemoryLeakDetector
    {
        private readonly Queue<MemorySnapshot> _snapshots = new();
        private readonly Dictionary<Type, int> _objectCounts = new();
        private readonly Dictionary<Type, float> _objectMemoryUsage = new();
        private const int MaxSnapshots = 10;
        private const float MemoryLeakThreshold = 0.2f; // 20% increase in memory usage

        public bool HasLeaks { get; private set; }

        public void TakeSnapshot()
        {
            var snapshot = new MemorySnapshot
            {
                Timestamp = DateTime.Now,
                ObjectCounts = new Dictionary<Type, int>(_objectCounts),
                ObjectMemoryUsage = new Dictionary<Type, float>(_objectMemoryUsage),
                TotalMemoryUsage = GC.GetTotalMemory(false) / (1024f * 1024f)
            };

            _snapshots.Enqueue(snapshot);
            if (_snapshots.Count > MaxSnapshots)
            {
                _snapshots.Dequeue();
            }

            CheckForLeaks();
        }

        public void AnalyzeLeaks()
        {
            if (_snapshots.Count < 2) return;

            var oldestSnapshot = _snapshots.Peek();
            var currentMemory = GC.GetTotalMemory(false) / (1024f * 1024f);
            var memoryIncrease = (currentMemory - oldestSnapshot.TotalMemoryUsage) / oldestSnapshot.TotalMemoryUsage;

            if (memoryIncrease > MemoryLeakThreshold)
            {
                HasLeaks = true;
                Debug.LogWarning($"[Redline Memory Master] Potential memory leak detected! Memory usage increased by {(memoryIncrease * 100):F1}% over {MaxSnapshots} snapshots");
                AnalyzeSuspiciousTypes();
            }
            else
            {
                HasLeaks = false;
            }
        }

        public void CheckForLeaks()
        {
            if (_snapshots.Count < 2) return;

            var oldestSnapshot = _snapshots.Peek();
            var currentMemory = GC.GetTotalMemory(false) / (1024f * 1024f);
            var memoryIncrease = (currentMemory - oldestSnapshot.TotalMemoryUsage) / oldestSnapshot.TotalMemoryUsage;

            if (memoryIncrease > MemoryLeakThreshold)
            {
                HasLeaks = true;
                Debug.LogWarning($"[Redline Memory Master] Potential memory leak detected! Memory usage increased by {(memoryIncrease * 100):F1}% over {MaxSnapshots} snapshots");
                AnalyzeSuspiciousTypes();
            }
            else
            {
                HasLeaks = false;
            }
        }

        public void AnalyzeSuspiciousTypes()
        {
            if (_snapshots.Count < 2) return;

            var oldestSnapshot = _snapshots.Peek();
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
                Debug.LogWarning("[Redline Memory Master] Suspicious object type increases:");
                foreach (var (type, increase) in suspiciousTypes.OrderByDescending(x => x.increase))
                {
                    Debug.LogWarning($"- {type.Name}: {increase * 100:F1}% increase in count");
                }
            }
        }

        public void UpdateObjectCount(Type type, int count)
        {
            _objectCounts[type] = count;
        }

        public void UpdateObjectMemoryUsage(Type type, float memoryUsage)
        {
            _objectMemoryUsage[type] = memoryUsage;
        }

        private class MemorySnapshot
        {
            public DateTime Timestamp;
            public Dictionary<Type, int> ObjectCounts;
            public Dictionary<Type, float> ObjectMemoryUsage;
            public float TotalMemoryUsage;
        }
    }
} 