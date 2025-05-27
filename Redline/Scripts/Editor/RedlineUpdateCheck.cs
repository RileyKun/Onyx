using System.IO;
using System.Net.Http;
using RedlineUpdater.Editor;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
    public class RedlineUpdateCheck: MonoBehaviour {
        [InitializeOnLoad]
        public class Startup {
            private
                const string VersionURL = "https://redline.arch-linux.pro/API/version.txt";

            private static readonly string CurrentVersion = RedlineVersionUtility.GetCurrentVersion();

            static Startup() {
                Check();
            }

            private static async void Check() {
                try {
                    // Validate local version first
                    var thisVersion = CurrentVersion;
                    if (!RedlineVersionUtility.IsValidSemanticVersion(thisVersion)) {
                        // Invalid local version format
                        Debug.LogError("[Redline] Invalid local version format detected. Please update to the latest version.");
                        EditorUtility.DisplayDialog(
                            "Redline Version Error",
                            "Your version of Redline has an unknown or invalid version number. Please install the latest version from the GitHub repository.",
                            "OK");
                        return;
                    }
                    
                    // Get server version
                    var httpClient = new HttpClient();
                    var result = await httpClient.GetAsync(VersionURL);
                    var strServerVersion = await result.Content.ReadAsStringAsync();
                    
                    // Validate server version
                    if (!RedlineVersionUtility.IsValidSemanticVersion(strServerVersion)) {
                        Debug.LogError($"[Redline] Invalid server version format: {strServerVersion}");
                        return;
                    }
                    
                    // Compare versions
                    int comparisonResult = RedlineVersionUtility.CompareVersions(thisVersion, strServerVersion);
                    
                    if (comparisonResult < 0) {
                        // Local version is older than server version - update needed
                        RedlineAutomaticUpdateAndInstall.AutomaticRedlineInstaller();
                    } 
                    else if (comparisonResult > 0) {
                        // Local version is newer than server version - development build
                        Debug.Log($"[Redline] Development Build Version Assumed (Local: {thisVersion}, Server: {strServerVersion})");
                    }
                    else {
                        // Versions are equal - up to date
                        Debug.Log($"[Redline] Up to date (Version: {thisVersion})");
                    }
                }
                catch (System.Exception ex) {
                    Debug.LogError($"[Redline] Error checking for updates: {ex.Message}");
                }
            }
        }
    }
}