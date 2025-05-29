using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Manages the vpm-manifest.json file for tracking installed packages
    /// </summary>
    public static class VPMManifestManager
    {
        private static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "vpm-manifest.json");

        /// <summary>
        /// Gets the current manifest data
        /// </summary>
        public static JObject GetManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                return new JObject
                {
                    ["dependencies"] = new JObject(),
                    ["locked"] = new JObject()
                };
            }

            try
            {
                string json = File.ReadAllText(ManifestPath);
                return JObject.Parse(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read vpm-manifest.json: {e.Message}");
                return new JObject
                {
                    ["dependencies"] = new JObject(),
                    ["locked"] = new JObject()
                };
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

            // Get all package directories
            string[] packageDirs = Directory.GetDirectories(packagesDir);
            foreach (string packageDir in packageDirs)
            {
                // Skip built-in Unity packages
                string dirName = Path.GetFileName(packageDir);
                if (dirName.StartsWith("com.unity."))
                    continue;

                // Check for package.json
                string packageJsonPath = Path.Combine(packageDir, "package.json");
                if (!File.Exists(packageJsonPath))
                    continue;

                try
                {
                    // Read package.json
                    string json = File.ReadAllText(packageJsonPath);
                    JObject packageJson = JObject.Parse(json);
                    
                    string packageId = packageJson["name"]?.ToString();
                    string installedVersion = packageJson["version"]?.ToString();

                    if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(installedVersion))
                        continue;

                    // Check if this package exists in our repositories
                    bool foundInRepos = false;
                    foreach (VPMRepository repo in VPMManager.GetRepositories())
                    {
                        if (repo.Packages != null && repo.Packages.TryGetValue(packageId, out VPMPackage package))
                        {
                            foundInRepos = true;
                            
                            // Get the latest version from the repository
                            VPMPackageVersion latestVersion = package.GetLatestVersion(true);
                            if (latestVersion != null)
                            {
                                // Update the manifest if the version is different
                                string currentVersion = GetInstalledVersion(packageId);
                                if (currentVersion != installedVersion)
                                {
                                    // Get dependencies from package.json if available
                                    Dictionary<string, string> dependencies = null;
                                    JObject deps = packageJson["dependencies"] as JObject;
                                    if (deps != null)
                                    {
                                        dependencies = new Dictionary<string, string>();
                                        foreach (var dep in deps)
                                        {
                                            dependencies[dep.Key] = dep.Value.ToString();
                                        }
                                    }

                                    AddOrUpdatePackage(packageId, installedVersion, dependencies);
                                    updatedCount++;
                                    Debug.Log($"Updated manifest for {packageId} from {currentVersion} to {installedVersion}");
                                }
                            }
                            break;
                        }
                    }

                    if (!foundInRepos)
                    {
                        Debug.Log($"Package {packageId} not found in any repository, skipping manifest update");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing package {dirName}: {e.Message}");
                }
            }

            return updatedCount;
        }
    }
} 