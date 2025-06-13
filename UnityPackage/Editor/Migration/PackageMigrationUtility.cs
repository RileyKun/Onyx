using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.Migration
{
    /// <summary>
    /// Utility to handle migration from the old package path (dev.runaxr.redline) to the new one (dev.redline-team.rpm)
    /// </summary>
    [InitializeOnLoad]
    public static class PackageMigrationUtility
    {
        private const string OldPackagePath = "dev.runaxr.redline";
        private const string NewPackagePath = "dev.redline-team.rpm";
        
        // Static constructor will be called when Unity loads/reloads scripts
        static PackageMigrationUtility()
        {
            // We want to run this check when the editor starts up
            EditorApplication.delayCall += CheckAndRemoveOldPackage;
        }

        /// <summary>
        /// Checks for the old package directory and removes it if found
        /// </summary>
        public static void CheckAndRemoveOldPackage()
        {
            try
            {
                // Get the Packages directory path
                string packagesDirectory = GetPackagesDirectory();
                if (string.IsNullOrEmpty(packagesDirectory))
                {
                    Debug.LogError("Could not determine Packages directory path.");
                    return;
                }

                string oldPackageFullPath = Path.Combine(packagesDirectory, OldPackagePath);
                
                // Check if the old package directory exists
                if (Directory.Exists(oldPackageFullPath))
                {
                    Debug.Log($"Found old package directory at: {oldPackageFullPath}");
                    
                    // Confirm with the user before deletion
                    if (EditorUtility.DisplayDialog(
                        "Package Migration Required",
                        $"The old package directory '{OldPackagePath}' has been found.\n\n" +
                        $"This directory needs to be removed to prevent conflicts with the new package '{NewPackagePath}'.\n\n" +
                        "Would you like to remove it now?",
                        "Yes, Remove It",
                        "No, I'll Handle It Manually"))
                    {
                        // Delete the directory
                        try
                        {
                            Directory.Delete(oldPackageFullPath, true);
                            Debug.Log($"Successfully removed old package directory: {oldPackageFullPath}");
                            
                            // Force Unity to refresh the asset database
                            AssetDatabase.Refresh();
                            
                            EditorUtility.DisplayDialog(
                                "Package Migration Complete",
                                $"The old package directory '{OldPackagePath}' has been successfully removed.\n\n" +
                                "The project will now use only the new package location.",
                                "OK");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to remove old package directory: {ex.Message}");
                            EditorUtility.DisplayDialog(
                                "Package Migration Failed",
                                $"Failed to remove the old package directory.\n\nError: {ex.Message}\n\n" +
                                "You may need to remove it manually.",
                                "OK");
                        }
                    }
                    else
                    {
                        Debug.Log("User chose to handle package migration manually.");
                    }
                }
                else
                {
                    // Old package not found, no action needed
                    Debug.Log($"Old package directory '{OldPackagePath}' not found. No migration needed.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during package migration check: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the directory where packages are installed
        /// </summary>
        /// <returns>The absolute path to the Packages directory</returns>
        private static string GetPackagesDirectory()
        {
            // Get the project's root directory
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectPath, "Packages");
        }

        /// <summary>
        /// Menu item to manually trigger the package migration check
        /// </summary>
        [MenuItem("Tools/Redline/Check for Package Migration")]
        public static void ManualCheckForMigration()
        {
            CheckAndRemoveOldPackage();
        }
    }
}
