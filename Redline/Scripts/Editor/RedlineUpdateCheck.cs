using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
    public class RedlineUpdateCheck : MonoBehaviour {
        // URL where the version info is stored on the server
        private const string VersionURL = "https://c0dera.in/Redline/api/version.txt";

        // Path to the local version file
        private static readonly string CurrentVersionFilePath = "Packages/dev.runaxr.Redline/RedlineUpdater/Editor/Redlineversion.txt";

        // Static constructor to trigger the update check when the editor starts
        static RedlineUpdateCheck() {
            // Only check version if the file exists
            if (File.Exists(CurrentVersionFilePath)) {
                CheckForUpdateAsync();
            } else {
                Debug.LogWarning("Local version file 'Redlineversion.txt' does not exist.");
            }
        }

        // Asynchronous method to check for an update
        private static async void CheckForUpdateAsync() {
            try {
                string currentVersion = File.ReadAllText(CurrentVersionFilePath).Trim();
                string serverVersion = await GetServerVersionAsync();

                // If the server version doesn't match the local version, trigger the update
                if (serverVersion != currentVersion) {
                    Debug.Log($"Redline Update available! Current: {currentVersion}, Server: {serverVersion}");
                    RedlineAutomaticUpdateAndInstall.AutomaticRedlineInstaller();
                } else {
                    Debug.Log("Redline is up to date.");
                }
            } catch (Exception ex) {
                Debug.LogError($"Error checking for Redline updates: {ex.Message}");
            }
        }

        // Asynchronously fetch the version string from the server
        private static async Task<string> GetServerVersionAsync() {
            using (var httpClient = new HttpClient()) {
                try {
                    HttpResponseMessage response = await httpClient.GetAsync(VersionURL);
                    response.EnsureSuccessStatusCode(); // Throw if not 2xx
                    return await response.Content.ReadAsStringAsync();
                } catch (HttpRequestException e) {
                    Debug.LogError($"HTTP request failed: {e.Message}");
                    return string.Empty; // Return empty string on failure
                } catch (Exception e) {
                    Debug.LogError($"Unexpected error: {e.Message}");
                    return string.Empty; // Return empty string on failure
                }
            }
        }
    }
}
