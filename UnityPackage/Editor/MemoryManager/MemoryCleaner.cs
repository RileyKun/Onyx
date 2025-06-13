using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.MemoryManager
{
    public class MemoryCleaner : IMemoryCleaner
    {
        private static readonly Dictionary<Type, Queue<object>> _objectPools = new();
        private static readonly Dictionary<Type, int> _poolSizes = new();
        private const int DefaultPoolSize = 100;

        private static readonly Dictionary<string, AssetBundle> _loadedAssetBundles = new();
        private static readonly Dictionary<string, DateTime> _assetBundleLastAccess = new();
        private const float AssetBundleUnloadTimeMinutes = 30f;

        public void PerformCleanup()
        {
            Debug.Log("[Redline Memory Master] Performing memory cleanup...");

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
            Debug.Log($"[Redline Memory Master] Cleanup complete. Memory reduced from {beforeMb:F2}MB to {afterMb:F2}MB (saved {beforeMb - afterMb:F2}MB)");
        }

        public void ClearPools()
        {
            foreach (var pool in _objectPools.Values)
            {
                pool.Clear();
            }
            _objectPools.Clear();
            _poolSizes.Clear();
        }

        public void OptimizeTextures()
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            foreach (var texture in textures)
            {
                if (texture == null) continue;

                // Skip if texture is already compressed
                if (texture.format == TextureFormat.RGBA32) continue;

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
                UnityEngine.Object.DestroyImmediate(tempTexture);
                UnityEngine.Object.DestroyImmediate(compressedTexture);
            }
        }

        public void UnloadUnusedAssetBundles()
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
                        Debug.Log($"[Redline Memory Master] Unloaded unused asset bundle: {bundleName}");
                    }
                    _loadedAssetBundles.Remove(bundleName);
                    _assetBundleLastAccess.Remove(bundleName);
                }
            }
        }
    }
} 