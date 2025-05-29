using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Static class for VPM system settings
    /// </summary>
    public static class VPMSettings
    {
        /// <summary>
        /// Whether to enable detailed debug logging
        /// </summary>
        public static bool EnableDebugLogging = false;
    }
    
    /// <summary>
    /// Represents a package in a VPM repository
    /// </summary>
    [Serializable]
    public class VPMPackage
    {
        /// <summary>
        /// The unique identifier of the package
        /// </summary>
        public string Id;
        
        /// <summary>
        /// Dictionary of available versions for this package
        /// </summary>
        public Dictionary<string, VPMPackageVersion> Versions = new Dictionary<string, VPMPackageVersion>();

        /// <summary>
        /// Determines if a version string represents a stable release (e.g. "1.2.3")
        /// or an unstable release (e.g. "1.2.3-beta.1", "1.2.3-rc.2")
        /// </summary>
        /// <param name="version">The version string to check</param>
        /// <returns>True if the version is stable, false otherwise</returns>
        public static bool IsStableVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;
            
            // Debug log to help diagnose version filtering (only if debug logging is enabled)
            if (VPMSettings.EnableDebugLogging)
                Debug.Log($"Checking if version is stable: {version}");
            
            // Check for any non-numeric characters (except dots)
            if (version.Any(c => !char.IsDigit(c) && c != '.'))
            {
                if (VPMSettings.EnableDebugLogging)
                    Debug.Log($"Version {version} is UNSTABLE (contains non-numeric characters)");
                return false;
            }
            
            // Try to parse as a simple version number
            try
            {
                // Split by dots and check if all parts are numeric
                string[] parts = version.Split('.');
                
                // Must have at least 2 parts (major.minor)
                if (parts.Length < 2)
                {
                    if (VPMSettings.EnableDebugLogging)
                        Debug.Log($"Version {version} is UNSTABLE (must have at least major.minor)");
                    return false;
                }
                
                // Must not have more than 3 parts (major.minor.patch)
                if (parts.Length > 3)
                {
                    if (VPMSettings.EnableDebugLogging)
                        Debug.Log($"Version {version} is UNSTABLE (must not have more than 3 parts)");
                    return false;
                }
                
                // Check each part
                foreach (string part in parts)
                {
                    // Each part must be a valid number
                    if (!int.TryParse(part, out int num) || num < 0)
                    {
                        if (VPMSettings.EnableDebugLogging)
                            Debug.Log($"Version {version} is UNSTABLE (contains invalid number: {part})");
                        return false;
                    }
                }
                
                if (VPMSettings.EnableDebugLogging)
                    Debug.Log($"Version {version} is STABLE");
                return true; // All parts are valid numbers
            }
            catch (Exception ex)
            {
                if (VPMSettings.EnableDebugLogging)
                    Debug.Log($"Version {version} is UNSTABLE (parsing error: {ex.Message})");
                return false; // Error parsing version
            }
        }
        
        /// <summary>
        /// Gets the latest version of the package based on semantic versioning
        /// </summary>
        /// <param name="includeUnstable">Whether to include unstable versions</param>
        /// <returns>The latest version, or null if no versions are available</returns>
        public VPMPackageVersion GetLatestVersion(bool includeUnstable = true)
        {
            if (Versions == null || Versions.Count == 0)
                return null;

            // Filter versions based on stability if needed
            IEnumerable<KeyValuePair<string, VPMPackageVersion>> filteredVersions = Versions;
            if (!includeUnstable)
            {
                filteredVersions = Versions.Where(v => IsStableVersion(v.Key));
                
                // If no stable versions found, return null
                if (!filteredVersions.Any())
                    return null;
            }

            // Try to sort versions using semantic versioning
            try
            {
                var sortedVersions = filteredVersions.OrderByDescending(v => 
                {
                    // For stable versions, use Version class
                    if (IsStableVersion(v.Key))
                    {
                        return new Version(v.Key);
                    }
                    
                    // For unstable versions, extract the base version
                    string baseVersion = v.Key.Split('-')[0];
                    try
                    {
                        return new Version(baseVersion);
                    }
                    catch
                    {
                        // If parsing fails, use a default version
                        return new Version(0, 0, 0);
                    }
                });

                return sortedVersions.First().Value;
            }
            catch (Exception)
            {
                // If sorting fails, just return the first version
                return filteredVersions.First().Value;
            }
        }

        /// <summary>
        /// Gets the latest version that is newer than the specified version
        /// </summary>
        /// <param name="currentVersion">The current version to compare against</param>
        /// <param name="includeUnstable">Whether to include unstable versions</param>
        /// <returns>The latest newer version, or null if no newer version is available</returns>
        public VPMPackageVersion GetLatestNewerVersion(string currentVersion, bool includeUnstable = true)
        {
            if (string.IsNullOrEmpty(currentVersion) || Versions == null || Versions.Count == 0)
                return null;

            try
            {
                Version currentVer = new Version(currentVersion.Split('-')[0]);
                
                // Filter versions based on stability if needed
                var filteredVersions = Versions.Where(v => 
                {
                    // First check if we should include unstable versions
                    if (!includeUnstable && !IsStableVersion(v.Key))
                        return false;

                    try
                    {
                        // For stable versions, compare directly
                        if (IsStableVersion(v.Key))
                        {
                            Version stableVer = new Version(v.Key);
                            return stableVer > currentVer;
                        }
                        
                        // For unstable versions, compare base versions
                        string baseVersion = v.Key.Split('-')[0];
                        Version unstableVer = new Version(baseVersion);
                        return unstableVer > currentVer;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (!filteredVersions.Any())
                    return null;

                // Sort by version and get the latest
                return filteredVersions
                    .OrderByDescending(v => new Version(v.Key.Split('-')[0]))
                    .First().Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets just the display name of the package without version
        /// </summary>
        /// <returns>The display name of the package</returns>
        public string GetDisplayName()
        {
            VPMPackageVersion latestVersion = GetLatestVersion();
            if (latestVersion != null)
            {
                return string.IsNullOrEmpty(latestVersion.DisplayName) ? Id : latestVersion.DisplayName;
            }
            
            return Id;
        }
        
        /// <summary>
        /// Gets the display name of the package with its latest version
        /// </summary>
        /// <returns>A formatted string with the display name and version</returns>
        public string GetDisplayNameWithVersion()
        {
            VPMPackageVersion latestVersion = GetLatestVersion();
            if (latestVersion != null)
            {
                string displayName = GetDisplayName();
                return $"{displayName} V{latestVersion.Version}";
            }
            
            return Id;
        }
    }
}
