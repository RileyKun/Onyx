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
                const string VersionURL = "https://c0dera.in/Redline/api/version.txt";

            private static readonly string CurrentVersion =
                File.ReadAllText("Packages/dev.runaxr.Redline/RedlineUpdater/Editor/Redlineversion.txt");

            static Startup() {
                Check();
            }

            private static async void Check() {
                var httpClient = new HttpClient();
                var result = await httpClient.GetAsync(VersionURL);
                var strServerVersion = await result.Content.ReadAsStringAsync();

                var thisVersion = CurrentVersion;

                if (strServerVersion != thisVersion) {
                    RedlineAutomaticUpdateAndInstall.AutomaticRedlineInstaller();
                }
            }
        }
    }
}