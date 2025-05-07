using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;

namespace Redline.Scripts.Editor {
    public abstract class RedlineImportManager {
        private const string V = "https://redline.arch-linux.pro/API/assets/";
        public const string ConfigName = "importConfig.json";
        public static string ServerUrl = V;
        private const string InternalServerUrl = V;

        // Starts the download and import process
        public static async void DownloadAndImportAssetFromServer(string assetName) {
            string assetPath = Path.Combine(RedlineSettings.GetAssetPath(), assetName);
            if (File.Exists(assetPath)) {
                RedlineLog($"{assetName} exists. Importing it..");
                ImportDownloadedAsset(assetPath);
            } else {
                RedlineLog($"{assetName} does not exist. Starting download..");
                await DownloadFile(assetName);
            }
        }

        // Download the asset file asynchronously
        private static async Task DownloadFile(string assetName) {
            string url = ServerUrl + assetName;
            string downloadPath = Path.Combine(RedlineSettings.GetAssetPath(), assetName);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url)) {
                webRequest.SetRequestHeader("User-Agent", "Webkit Gecko wHTTPS (Keep Alive 55)");

                // Start download
                var operation = webRequest.SendWebRequest();

                // While downloading, show the progress
                while (!operation.isDone) {
                    EditorUtility.DisplayProgressBar("Downloading " + assetName,
                        "Downloading " + assetName + ". Currently at: " + (int)(operation.progress * 100) + "%",
                        operation.progress);
                    await Task.Yield();  // Yield control back to the Unity editor thread
                }

                if (webRequest.result == UnityWebRequest.Result.Success) {
                    // Ensure directory exists before writing
                    Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));
                    File.WriteAllBytes(downloadPath, webRequest.downloadHandler.data);
                    RedlineLog($"Download of {assetName} completed!");
                    ImportDownloadedAsset(downloadPath);
                } else {
                    RedlineLog($"Download of {assetName} failed! Error: {webRequest.error}");
                }

                EditorUtility.ClearProgressBar();
            }
        }

        // Delete the asset from the system
        public static void DeleteAsset(string assetName) {
            string assetPath = Path.Combine(RedlineSettings.GetAssetPath(), assetName);
            if (File.Exists(assetPath)) {
                File.Delete(assetPath);
                RedlineLog($"{assetName} deleted.");
            }
        }

        // Update the configuration file
        public static async void UpdateConfig() {
            string url = InternalServerUrl + ConfigName;
            string updatePath = Path.Combine(RedlineSettings.ProjectConfigPath, "update_" + ConfigName);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url)) {
                webRequest.SetRequestHeader("User-Agent", "Webkit Gecko wHTTPS (Keep Alive 55)");

                // Start download
                var operation = webRequest.SendWebRequest();

                // While downloading, show the progress
                while (!operation.isDone) {
                    EditorUtility.DisplayProgressBar("Downloading Config", "Downloading import config...", operation.progress);
                    await Task.Yield();
                }

                if (webRequest.result == UnityWebRequest.Result.Success) {
                    // Ensure directory exists before writing
                    Directory.CreateDirectory(Path.GetDirectoryName(updatePath));
                    File.WriteAllBytes(updatePath, webRequest.downloadHandler.data);
                    RedlineLog("Config download completed!");

                    // Replace old config file
                    string oldConfigPath = Path.Combine(RedlineSettings.ProjectConfigPath, ConfigName);
                    if (File.Exists(oldConfigPath)) {
                        File.Delete(oldConfigPath);
                    }

                    File.Move(updatePath, oldConfigPath);
                    RedlinePackageManager.LoadJson();
                    EditorPrefs.SetInt("Redline_configImportLastUpdated", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                } else {
                    RedlineLog("Config download failed! Error: " + webRequest.error);
                }

                EditorUtility.ClearProgressBar();
            }
        }

        // Check for updates to the configuration file
        public static void CheckForConfigUpdate() {
            if (EditorPrefs.HasKey("Redline_configImportLastUpdated")) {
                var lastUpdated = EditorPrefs.GetInt("Redline_configImportLastUpdated");
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Skip update if the config was recently updated within the last hour
                if (currentTime - lastUpdated < 3600) {
                    Debug.Log("Not updating config: " + (currentTime - lastUpdated) + " seconds since last update.");
                    return;
                }
            }

            RedlineLog("Updating import config");
            UpdateConfig();
        }

        // Log function for Redline operations
        private static void RedlineLog(string message) {
            Debug.Log("[Redline] AssetDownloadManager: " + message);
        }

        // Import the downloaded asset package
        private static void ImportDownloadedAsset(string assetPath) {
            AssetDatabase.ImportPackage(assetPath, true);
        }
    }
}
