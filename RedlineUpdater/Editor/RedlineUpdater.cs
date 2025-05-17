using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RedlineUpdater.Editor {
    /// <summary>
    /// Handles automatic updates for the Redline package manager.
    /// </summary>
    public class RedlineAutomaticUpdateAndInstall : MonoBehaviour {
        #region Constants
        private const string VERSION_URL = "https://redline.arch-linux.pro/API/version.txt";
        private const string PACKAGE_URL = "https://redline.arch-linux.pro/API/assets/latest/Redline.unitypackage";
        private const string LAST_DECLINED_VERSION_KEY = "Redline_LastDeclinedVersion";
        private const string ASSET_NAME = "Redline.unitypackage";
        private const string TOOLKIT_PATH = "Packages/dev.redline-team.rpm";
        private const string VERSION_FILE_PATH = "Packages/dev.redline-team.rpm/RedlineUpdater/Editor/RedlineVersion.txt";
        private const string LOG_PREFIX = "[Redline] AssetDownloadManager: ";
        #endregion

        #region Properties
        private static readonly string CurrentVersion = File.ReadAllText(VERSION_FILE_PATH);
        private static readonly HttpClient HttpClient = new HttpClient();
        #endregion

        /// <summary>
        /// Checks for updates and installs them if available and approved by the user.
        /// </summary>
        public static async void AutomaticRedlineInstaller() {
            try {
                string serverVersion = await GetServerVersion();
                if (string.IsNullOrEmpty(serverVersion)) return;

                if (IsUpToDate(serverVersion)) {
                    HandleUpToDateVersion();
                } else {
                    await HandleOutdatedVersion();
                }
            } catch (Exception ex) {
                LogError(ex.Message);
            }
        }

        #region Version Checking
        private static async Task<string> GetServerVersion() {
            try {
                var response = await HttpClient.GetAsync(VERSION_URL);
                return await response.Content.ReadAsStringAsync();
            } catch (Exception ex) {
                LogError($"Failed to get server version: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool IsUpToDate(string serverVersion) {
            return CurrentVersion == serverVersion;
        }

        private static void HandleUpToDateVersion() {
            Log("Alright we're up to date!");
            EditorPrefs.DeleteKey(LAST_DECLINED_VERSION_KEY);
        }

        private static async Task HandleOutdatedVersion() {
            Log("There is an Update Available");
            await ProcessUpdate();
        }
        #endregion

        #region Update Processing
        private static async Task ProcessUpdate() {
            if (WasUpdatePreviouslyDeclined()) {
                Log("Update was previously declined for this version");
                return;
            }

            if (await PromptForUpdate()) {
                await PerformUpdate();
            } else {
                HandleUpdateDeclined();
            }
        }

        private static bool WasUpdatePreviouslyDeclined() {
            string lastDeclinedVersion = EditorPrefs.GetString(LAST_DECLINED_VERSION_KEY, string.Empty);
            return lastDeclinedVersion == CurrentVersion;
        }

        private static Task<bool> PromptForUpdate() {
            Log("Asking for Approval..");
            bool userApproved = EditorUtility.DisplayDialog(
                "Redline Updater",
                $"Your version (V{CurrentVersion}) is outdated from the repo! Do you wish to update?",
                "Yes", "No");

            return Task.FromResult(userApproved);
        }

        private static void HandleUpdateDeclined() {
            Log("Update cancelled...");
            EditorPrefs.SetString(LAST_DECLINED_VERSION_KEY, CurrentVersion);
        }
        #endregion

        #region Update Execution
        private static async Task PerformUpdate() {
            if (!ConfirmUpdateWarning()) return;

            if (await DeleteExistingFiles()) {
                RefreshAssetDatabase();
                DownloadAndInstallNewVersion();
            }
        }

        private static bool ConfirmUpdateWarning() {
            return EditorUtility.DisplayDialog(
                "Redline Updater",
                "Updater will now attempt to update the package manager. We would recommend backing up your project files in case something fails!",
                "OK");
        }

        private static async Task<bool> DeleteExistingFiles() {
            try {
                string[] files = Directory.GetFiles(TOOLKIT_PATH, "*.*");
                await Task.Run(() => {
                    foreach (string file in files) {
                        Log($"File {file} was deleted");
                        File.Delete(file);
                    }
                });
                Log("Files deleted...");
                return true;
            } catch (DirectoryNotFoundException) {
                HandleDeletionError();
                return false;
            } catch (Exception ex) {
                LogError($"Error deleting files: {ex.Message}");
                return false;
            }
        }

        private static void HandleDeletionError() {
            Log("Update failed...");
            EditorUtility.DisplayDialog(
                "Error Deleting Files",
                "Failed to update Redline! If this error persists, update Redline manually from the GitHub repository!",
                "OK");
        }

        private static void RefreshAssetDatabase() {
            AssetDatabase.Refresh();
        }

        private static void DownloadAndInstallNewVersion() {
            if (!ConfirmInstallation()) return;

            try {
                var webClient = new WebClient();
                ConfigureWebClient(webClient);
                webClient.DownloadFileAsync(new Uri(PACKAGE_URL), ASSET_NAME);
            } catch (Exception ex) {
                LogError($"Error starting download: {ex.Message}");
                OfferManualDownload();
            }
        }

        private static bool ConfirmInstallation() {
            return EditorUtility.DisplayDialog(
                "Redline_Automatic_DownloadAndInstall", 
                "Alright we're installing the new RPM now",
                "Nice!");
        }

        private static void ConfigureWebClient(WebClient client) {
            client.Headers.Set(HttpRequestHeader.UserAgent, "Webkit Gecko wHTTPS (Keep Alive 55)");
            client.DownloadFileCompleted += FileDownloadComplete;
            client.DownloadProgressChanged += FileDownloadProgress;
        }
        #endregion

        #region Download Handlers
        private static void FileDownloadProgress(object sender, DownloadProgressChangedEventArgs e) {
            int progress = e.ProgressPercentage;
            
            if (progress < 0) {
                return;
            } else if (progress >= 100) {
                EditorUtility.ClearProgressBar();
            } else {
                EditorUtility.DisplayProgressBar(
                    $"Download of {ASSET_NAME}",
                    $"Downloading {ASSET_NAME} {progress}%",
                    progress / 100f);
            }
        }

        private static void FileDownloadComplete(object sender, AsyncCompletedEventArgs e) {
            if (e.Error == null) {
                HandleSuccessfulDownload();
            } else {
                HandleFailedDownload();
            }
        }

        private static void HandleSuccessfulDownload() {
            Log("Download completed!");
            Process.Start(ASSET_NAME);
        }

        private static void HandleFailedDownload() {
            Log("Download failed!");
            OfferManualDownload();
        }

        private static void OfferManualDownload() {
            if (EditorUtility.DisplayDialog(
                "Redline_Automatic_DownloadAndInstall", 
                "Something screwed up and we couldn't download the latest Redline",
                "Open URL instead", "Cancel")) {
                Application.OpenURL(PACKAGE_URL);
            }
        }
        #endregion

        #region Logging
        private static void Log(string message) {
            Debug.Log(LOG_PREFIX + message);
        }

        private static void LogError(string message) {
            Debug.LogError(LOG_PREFIX + message);
        }
        #endregion
    }
}