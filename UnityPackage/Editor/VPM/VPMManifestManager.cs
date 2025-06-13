using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Manages the vpm-manifest.json file for tracking installed packages
    /// </summary>
    public static class VPMManifestManager
    {
        private static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "vpm-manifest.json");
        
        // Cache for manifest data
        private static JObject _cachedManifest;
        private static DateTime _lastManifestRead;
        private const int MANIFEST_CACHE_LIFETIME_SECONDS = 5;

        /// <summary>
        /// Gets the current manifest data
        /// </summary>
        public static JObject GetManifest()
        {
            // Check if cache is valid
            if (_cachedManifest != null && 
                (DateTime.Now - _lastManifestRead).TotalSeconds < MANIFEST_CACHE_LIFETIME_SECONDS)
            {
                return _cachedManifest;
            }

            if (!File.Exists(ManifestPath))
            {
                _cachedManifest = new JObject
                {
                    ["dependencies"] = new JObject(),
                    ["locked"] = new JObject()
                };
                _lastManifestRead = DateTime.Now;
                return _cachedManifest;
            }

            try
            {
                string json = File.ReadAllText(ManifestPath);
                _cachedManifest = JObject.Parse(json);
                _lastManifestRead = DateTime.Now;
                return _cachedManifest;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read vpm-manifest.json: {e.Message}");
                _cachedManifest = new JObject
                {
                    ["dependencies"] = new JObject(),
                    ["locked"] = new JObject()
                };
                _lastManifestRead = DateTime.Now;
                return _cachedManifest;
            }
        }

        /// <summary>
        /// Saves the manifest data to disk
        /// </summary>
        private static void SaveManifest(JObject manifest)
        {
            try
            {
                string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                File.WriteAllText(ManifestPath, json);
                
                // Update cache
                _cachedManifest = manifest;
                _lastManifestRead = DateTime.Now;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write vpm-manifest.json: {e.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a package in the manifest
        /// </summary>
        public static void AddOrUpdatePackage(string packageId, string version, Dictionary<string, string> dependencies = null)
        {
            JObject manifest = GetManifest();
            
            // Update dependencies section
            JObject dependenciesObj = (JObject)manifest["dependencies"];
            dependenciesObj[packageId] = new JObject
            {
                ["version"] = version
            };

            // Update locked section
            JObject lockedObj = (JObject)manifest["locked"];
            JObject packageLock = new JObject
            {
                ["version"] = version
            };

            if (dependencies != null && dependencies.Count > 0)
            {
                JObject depsObj = new JObject();
                foreach (var dep in dependencies)
                {
                    depsObj[dep.Key] = dep.Value;
                }
                packageLock["dependencies"] = depsObj;
            }
            else
            {
                packageLock["dependencies"] = new JObject();
            }

            lockedObj[packageId] = packageLock;

            SaveManifest(manifest);
        }

        /// <summary>
        /// Removes a package from the manifest
        /// </summary>
        public static void RemovePackage(string packageId)
        {
            JObject manifest = GetManifest();
            
            // Remove from dependencies
            JObject dependenciesObj = (JObject)manifest["dependencies"];
            dependenciesObj.Remove(packageId);

            // Remove from locked
            JObject lockedObj = (JObject)manifest["locked"];
            lockedObj.Remove(packageId);

            SaveManifest(manifest);
        }

        /// <summary>
        /// Gets the installed version of a package from the manifest
        /// </summary>
        public static string GetInstalledVersion(string packageId)
        {
            JObject manifest = GetManifest();
            JObject dependenciesObj = (JObject)manifest["dependencies"];
            
            if (dependenciesObj.TryGetValue(packageId, out JToken packageObj))
            {
                return packageObj["version"]?.ToString();
            }
            
            return null;
        }

        /// <summary>
        /// Gets all installed packages from the manifest
        /// </summary>
        public static Dictionary<string, string> GetInstalledPackages()
        {
            JObject manifest = GetManifest();
            JObject dependenciesObj = (JObject)manifest["dependencies"];
            
            Dictionary<string, string> packages = new Dictionary<string, string>();
            foreach (var prop in dependenciesObj.Properties())
            {
                packages[prop.Name] = prop.Value["version"]?.ToString();
            }
            
            return packages;
        }

        /// <summary>
        /// Scans installed packages and updates the manifest if needed
        /// </summary>
        /// <returns>Number of packages that were updated</returns>
        public static int ScanAndUpdateManifest()
        {
            int updatedCount = 0;
            string packagesDir = Path.Combine(Application.dataPath, "..", "Packages");
            
            if (!Directory.Exists(packagesDir))
                return 0;

            foreach (string packageDir in Directory.GetDirectories(packagesDir))
            {
                string packageJsonPath = Path.Combine(packageDir, "package.json");
                if (!File.Exists(packageJsonPath))
                    continue;

                try
                {
                    string json = File.ReadAllText(packageJsonPath);
                    JObject packageJson = JObject.Parse(json);
                    
                    string packageId = packageJson["name"]?.ToString();
                    string installedVersion = packageJson["version"]?.ToString();

                    if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(installedVersion))
                        continue;

                    // Get dependencies
                    Dictionary<string, string> dependencies = null;
                    JObject depsObj = packageJson["dependencies"] as JObject;
                    if (depsObj != null)
                    {
                        dependencies = new Dictionary<string, string>();
                        foreach (var dep in depsObj)
                        {
                            dependencies[dep.Key] = dep.Value.ToString();
                        }
                    }

                    // Update manifest if needed
                    string currentVersion = GetInstalledVersion(packageId);
                    if (currentVersion != installedVersion)
                    {
                        AddOrUpdatePackage(packageId, installedVersion, dependencies);
                        updatedCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing package.json in {packageDir}: {e.Message}");
                }
            }

            return updatedCount;
        }

        /// <summary>
        /// Asynchronously scans installed packages and updates the manifest if needed
        /// </summary>
        /// <returns>Task containing the number of packages that were updated</returns>
        public static async Task<int> ScanAndUpdateManifestAsync()
        {
            return await Task.Run(() => ScanAndUpdateManifest());
        }
    }
} 