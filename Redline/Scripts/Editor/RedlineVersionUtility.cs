using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Redline.Scripts.Editor
{
    /// <summary>
    /// Utility class for retrieving Redline version information from package.json
    /// </summary>
    public static class RedlineVersionUtility
    {
        private const string PackageJsonPath = "Packages/dev.redline-team.rpm/package.json";
        
        /// <summary>
        /// Gets the current version of Redline from package.json
        /// </summary>
        /// <returns>The version string (e.g., "3.2.2")</returns>
        public static string GetCurrentVersion()
        {
            try
            {
                if (File.Exists(PackageJsonPath))
                {
                    string jsonContent = File.ReadAllText(PackageJsonPath);
                    
                    // Parse the JSON to get the version
                    // Using simple string parsing to avoid adding dependencies
                    const string versionTag = "\"version\": \"";
                    int versionStartIndex = jsonContent.IndexOf(versionTag) + versionTag.Length;
                    int versionEndIndex = jsonContent.IndexOf("\"", versionStartIndex);
                    
                    if (versionStartIndex >= versionTag.Length && versionEndIndex > versionStartIndex)
                    {
                        return jsonContent.Substring(versionStartIndex, versionEndIndex - versionStartIndex);
                    }
                }
                
                Debug.LogError("Failed to read Redline version from package.json");
                return "Unknown";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error reading Redline version: {ex.Message}");
                return "Unknown";
            }
        }
        
        /// <summary>
        /// Checks if a version string follows semantic versioning format (X.Y.Z)
        /// </summary>
        /// <param name="version">Version string to validate</param>
        /// <returns>True if the version follows semantic versioning format</returns>
        public static bool IsValidSemanticVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;
                
            // Basic semantic version regex pattern (X.Y.Z)
            var regex = new Regex(@"^\d+\.\d+\.\d+$");
            return regex.IsMatch(version);
        }
        
        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version</param>
        /// <param name="version2">Second version</param>
        /// <returns>-1 if version1 is older, 0 if equal, 1 if version1 is newer</returns>
        public static int CompareVersions(string version1, string version2)
        {
            if (!IsValidSemanticVersion(version1) || !IsValidSemanticVersion(version2))
                return -99; // Invalid comparison
                
            var v1Parts = version1.Split('.');
            var v2Parts = version2.Split('.');
            
            // Compare major version
            int major1 = int.Parse(v1Parts[0]);
            int major2 = int.Parse(v2Parts[0]);
            
            if (major1 != major2)
                return major1.CompareTo(major2);
                
            // Compare minor version
            int minor1 = int.Parse(v1Parts[1]);
            int minor2 = int.Parse(v2Parts[1]);
            
            if (minor1 != minor2)
                return minor1.CompareTo(minor2);
                
            // Compare patch version
            int patch1 = int.Parse(v1Parts[2]);
            int patch2 = int.Parse(v2Parts[2]);
            
            return patch1.CompareTo(patch2);
        }
    }
}
