using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace RedlineUpdater.Editor
{
    public class RedlineAutomaticUpdateAndInstall : MonoBehaviour
    {
        // Define the URLs and paths
        private const string VersionURL = "https://c0dera.in/Redline/api/version.txt";
        private const string UnitypackageUrl = "https://c0dera.in/Redline/api/assets/latest/Redline.unitypackage";
        private static readonly string CurrentVersion = File.ReadAllText("Packages/dev.runaxr.Redline/RedlineUpdater/editor/RedlineVersion.txt");
        private const string AssetName = "Redline.unitypackage";
        private const string ToolkitPath = @"Packages\dev.runaxr.redline";

        // Starting the automatic update process
        public static async void AutomaticRedlineInstaller()
        {
            using (UnityWebRequest www = UnityWebRequest.Get(VersionURL))
            {
                // Make an HTTP request to fetch the version from the server
                await www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string serverVersion = www.downloadHandler.text;
                    if (string.IsNullOrEmpty(serverVersion)) return;

                    // Check if the version is up to date
                    if (CurrentVersion == serverVersion)
                    {
                        RedlineLog("Alright we're up to date!");
                    }
                    else
                    {
                        RedlineLog("There is an Update Available");
                        // Start downloading the update
                        await DownloadRedline();
                    }
                }
                else
                {
                    Debug.LogError("[Redline] AssetDownloadManager: " + www.error);
                }
            }
        }

        private static async Task DownloadRedline()
        {
            RedlineLog("Asking for Approval..");

            // Simulating the user prompt for update confirmation
            bool shouldUpdate = EditorUtility.DisplayDialog("Redline Updater",
                $"Your version (V{CurrentVersion}) is outdated! Do you wish to update?", "Yes", "No");

            if (shouldUpdate)
            {
                await DeleteAndDownloadAsync();
            }
            else
            {
                RedlineLog("Update cancelled...");
            }
        }

        private static async Task DeleteAndDownloadAsync()
        {
            bool proceedWithDeletion = EditorUtility.DisplayDialog("Redline Updater",
                "Updater will now attempt to update the package manager. We recommend backing up your project files in case something fails!", "OK");

            if (proceedWithDeletion)
            {
                try
                {
                    // Delete files in the toolkit directory
                    var toolkitDir = Directory.GetFiles(ToolkitPath);
                    foreach (var file in toolkitDir)
                    {
                        File.Delete(file);
                        RedlineLog($"File {file} was deleted");
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    RedlineLog("Update failed...");
                    EditorUtility.DisplayDialog("Error Deleting Files", 
                        "Failed to update Redline! If this error persists, update Redline manually from the GitHub repository!", "OK");
                    return;
                }
            }

            RedlineLog("Files deleted...");

            // Refresh the assets in the Unity editor to reflect the new changes
            AssetDatabase.Refresh();

            // Proceed with downloading the .unitypackage
            bool confirmDownload = EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", 
                "Alright, we're installing the new RPM now", "Nice!");

            if (confirmDownload)
            {
                using (UnityWebRequest download = UnityWebRequest.Get(UnitypackageUrl))
                {
                    string downloadPath = Path.Combine(Application.dataPath, AssetName);
                    download.downloadHandler = new DownloadHandlerFile(downloadPath);
                    await download.SendWebRequest();

                    if (download.result == UnityWebRequest.Result.Success)
                    {
                        RedlineLog("Download completed!");
                        Process.Start(downloadPath); // Automatically open the .unitypackage file
                    }
                    else
                    {
                        RedlineLog("Download failed!");
                        if (EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", 
                            "Something went wrong, and we couldn't download the latest Redline. Would you like to open the URL manually?", 
                            "Open URL", "Cancel"))
                        {
                            Application.OpenURL(UnitypackageUrl);
                        }
                    }
                }
            }
        }

        private static void RedlineLog(string message)
        {
            Debug.Log("[Redline] AssetDownloadManager: " + message);
        }
    }
}
