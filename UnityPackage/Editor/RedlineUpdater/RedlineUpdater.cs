using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Redline.Editor;

[Serializable]
public class GitHubRelease
{
    public string tag_name;
    public string name;
    public bool draft;
    public bool prerelease;
    public DateTime published_at;
    public string html_url;
}

namespace Redline.Editor.RedlineUpdater {
    /// <summary>
    /// Handles automatic updates for the Redline package manager.
    /// </summary>
    public class RedlineAutomaticUpdateAndInstall : MonoBehaviour {
        #region Constants
        private const string GITHUB_API_URL = "https://api.github.com/repos/Redline-Team/RPM/releases/latest";
        private const string GITHUB_RELEASE_URL = "https://github.com/Redline-Team/RPM/releases/latest/download/Redline_{0}.unitypackage";
        private const string LAST_DECLINED_VERSION_KEY = "Redline_LastDeclinedVersion";
        private const string ASSET_NAME = "Redline.unitypackage";
        private const string TOOLKIT_PATH = "Packages/dev.redline-team.rpm";
        private const string LOG_PREFIX = "[Redline] AssetDownloadManager: ";
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 1000;
        #endregion

        #region Properties
        private static readonly string CurrentVersion = RedlineVersionUtility.GetCurrentVersion();
        private static readonly HttpClient HttpClient = new HttpClient {
            Timeout = TimeSpan.FromSeconds(30)
        };
        #endregion

        /// <summary>
        /// Checks for updates and installs them if available and approved by the user.
        /// </summary>
        public static async void AutomaticRedlineInstaller() {
            try {
                string serverVersion = await GetServerVersionWithRetry();
                if (string.IsNullOrEmpty(serverVersion)) {
                    LogError("Failed to retrieve server version after multiple attempts");
                    return;
                }

                if (IsUpToDate(serverVersion)) {
                    HandleUpToDateVersion(serverVersion);
                } else {
                    await HandleOutdatedVersion(serverVersion);
                }
            } catch (Exception ex) {
                LogError($"Update process failed: {ex.Message}");
                OfferManualDownload();
            }
        }

        #region Version Checking
        private static async Task<string> GetServerVersionWithRetry() {
            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++) {
                try {
                    HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RedlineUpdater/1.0");
                    using var response = await HttpClient.GetAsync(GITHUB_API_URL);
                    response.EnsureSuccessStatusCode();
                    
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var version = JsonUtility.FromJson<GitHubRelease>(jsonResponse)?.tag_name;
                    
                    // Remove 'v' prefix if present
                    if (!string.IsNullOrEmpty(version) && version.StartsWith("v")) {
                        version = version.Substring(1);
                    }
                    
                    return version;
                } catch (Exception ex) {
                    LogError($"Attempt {attempt}/{MAX_RETRY_ATTEMPTS} failed: {ex.Message}");
                    if (attempt < MAX_RETRY_ATTEMPTS) {
                        await Task.Delay(RETRY_DELAY_MS);
                    }
                }
            }
            return string.Empty;
        }

        private static bool IsUpToDate(string serverVersion) {
            if (!RedlineVersionUtility.IsValidSemanticVersion(CurrentVersion) || 
                !RedlineVersionUtility.IsValidSemanticVersion(serverVersion)) {
                LogError("Invalid version format detected");
                return false;
            }
            
            int comparisonResult = RedlineVersionUtility.CompareVersions(CurrentVersion, serverVersion);
            return comparisonResult >= 0;
        }

        private static void HandleUpToDateVersion(string serverVersion) {
            if (RedlineVersionUtility.CompareVersions(CurrentVersion, serverVersion) > 0) {
                Log($"Development Build Version Detected (Local: {CurrentVersion}, Server: {serverVersion})");
            } else {
                Log($"Package is up to date (Version: {CurrentVersion})");
            }
            EditorPrefs.DeleteKey(LAST_DECLINED_VERSION_KEY);
        }

        private static async Task HandleOutdatedVersion(string serverVersion) {
            Log($"Update available: Current version {CurrentVersion} -> Server version {serverVersion}");
            await ProcessUpdate(serverVersion);
        }
        #endregion

        #region Update Processing
        private static async Task ProcessUpdate(string serverVersion) {
            if (WasUpdatePreviouslyDeclined(serverVersion)) {
                Log("Update was previously declined for this version");
                return;
            }

            if (await PromptForUpdate(serverVersion)) {
                await PerformUpdate();
            } else {
                HandleUpdateDeclined(serverVersion);
            }
        }

        private static bool WasUpdatePreviouslyDeclined(string serverVersion) {
            string lastDeclinedVersion = EditorPrefs.GetString(LAST_DECLINED_VERSION_KEY, string.Empty);
            return lastDeclinedVersion == serverVersion;
        }

        private static Task<bool> PromptForUpdate(string serverVersion) {
            Log("Requesting user approval for update...");
            bool userApproved = EditorUtility.DisplayDialog(
                "Redline Updater",
                $"A new version of Redline is available:\n\nCurrent: {CurrentVersion}\nAvailable: {serverVersion}\n\nWould you like to update now?",
                "Update Now", "Later");

            return Task.FromResult(userApproved);
        }

        private static void HandleUpdateDeclined(string serverVersion) {
            Log("Update declined by user");
            EditorPrefs.SetString(LAST_DECLINED_VERSION_KEY, serverVersion);
        }
        #endregion

        #region Update Execution
        private static async Task PerformUpdate() {
            if (!ConfirmUpdateWarning()) return;

            try {
                if (await DeleteExistingFiles()) {
                    RefreshAssetDatabase();
                    await DownloadAndInstallNewVersion();
                }
            } catch (Exception ex) {
                LogError($"Update failed: {ex.Message}");
                OfferManualDownload();
            }
        }

        private static bool ConfirmUpdateWarning() {
            return EditorUtility.DisplayDialog(
                "Redline Updater",
                "The updater will now attempt to update the package manager. It is recommended to back up your project files before proceeding.\n\nDo you want to continue?",
                "Continue", "Cancel");
        }

        private static async Task<bool> DeleteExistingFiles() {
            try {
                if (!Directory.Exists(TOOLKIT_PATH)) {
                    LogError($"Directory not found: {TOOLKIT_PATH}");
                    return false;
                }

                string[] files = Directory.GetFiles(TOOLKIT_PATH, "*.*", SearchOption.AllDirectories);
                await Task.Run(() => {
                    foreach (string file in files) {
                        try {
                            File.Delete(file);
                            Log($"Deleted: {file}");
                        } catch (Exception ex) {
                            LogError($"Failed to delete {file}: {ex.Message}");
                        }
                    }
                });
                Log("All files deleted successfully");
                return true;
            } catch (Exception ex) {
                LogError($"Error during file deletion: {ex.Message}");
                return false;
            }
        }

        private static void RefreshAssetDatabase() {
            AssetDatabase.Refresh();
        }

        private static async Task DownloadAndInstallNewVersion() {
            if (!ConfirmInstallation()) return;

            try {
                // First get the latest version to construct the correct asset URL
                string serverVersion = await GetServerVersionWithRetry();
                if (string.IsNullOrEmpty(serverVersion)) {
                    throw new Exception("Failed to get server version");
                }

                // Construct the asset URL with the version
                string assetUrl = string.Format(GITHUB_RELEASE_URL, $"v{serverVersion}");
                
                using var webClient = new WebClient();
                ConfigureWebClient(webClient);
                
                var downloadTask = new TaskCompletionSource<bool>();
                webClient.DownloadFileCompleted += (s, e) => {
                    if (e.Error != null) {
                        downloadTask.SetException(e.Error);
                    } else {
                        downloadTask.SetResult(true);
                    }
                };

                Log($"Downloading asset from: {assetUrl}");
                webClient.DownloadFileAsync(new Uri(assetUrl), ASSET_NAME);
                await downloadTask.Task;
            } catch (Exception ex) {
                LogError($"Download failed: {ex.Message}");
                OfferManualDownload();
            }
        }

        private static bool ConfirmInstallation() {
            return EditorUtility.DisplayDialog(
                "Redline Updater", 
                "Ready to install the new version of Redline. The package will be downloaded and imported automatically.",
                "Proceed", "Cancel");
        }

        private static void ConfigureWebClient(WebClient client) {
            client.Headers.Set(HttpRequestHeader.UserAgent, "RedlineUpdater/1.0");
            client.DownloadProgressChanged += FileDownloadProgress;
        }
        #endregion

        #region Download Handlers
        private static void FileDownloadProgress(object sender, DownloadProgressChangedEventArgs e) {
            int progress = e.ProgressPercentage;
            
            if (progress < 0) return;
            
            if (progress >= 100) {
                EditorUtility.ClearProgressBar();
            } else {
                EditorUtility.DisplayProgressBar(
                    "Redline Updater",
                    $"Downloading update... {progress}%",
                    progress / 100f);
            }
        }

        private static void HandleSuccessfulDownload() {
            Log("Download completed successfully");
            try {
                Process.Start(ASSET_NAME);
            } catch (Exception ex) {
                LogError($"Failed to start installation: {ex.Message}");
                OfferManualDownload();
            }
        }

        private static async void OfferManualDownload() {
            try {
                // Get the latest version to construct the correct download URL
                string serverVersion = await GetServerVersionWithRetry();
                if (string.IsNullOrEmpty(serverVersion)) {
                    // Fallback to GitHub releases page if we can't get the version
                    Application.OpenURL("https://github.com/Redline-Team/RPM/releases/latest");
                    return;
                }
                
                string downloadUrl = string.Format(GITHUB_RELEASE_URL, $"v{serverVersion}");
                Application.OpenURL(downloadUrl);
            } catch {
                // Fallback to GitHub releases page if anything goes wrong
                Application.OpenURL("https://github.com/Redline-Team/RPM/releases/latest");
            }
        }
        #endregion

        #region Logging
        private static void Log(string message) {
            Debug.Log($"{LOG_PREFIX}{message}");
        }

        private static void LogError(string message) {
            Debug.LogError($"{LOG_PREFIX}{message}");
        }
        #endregion
    }
}