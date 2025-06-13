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

            // Check if the version contains any pre-release identifiers
            return !version.Contains("-");
        }

        /// <summary>
        /// Gets the latest version of the package
        /// </summary>
        /// <param name="includeUnstable">Whether to include unstable versions</param>
        /// <returns>The latest version, or null if no versions are available</returns>
        public VPMPackageVersion GetLatestVersion(bool includeUnstable = true)
        {
            if (Versions == null || Versions.Count == 0)
                return null;

            return Versions.Values
                .Where(v => includeUnstable || IsStableVersion(v.Version))
                .OrderByDescending(v => new Version(v.Version.Split('-')[0]))
                .FirstOrDefault();
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
