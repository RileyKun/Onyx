using dev.runaxr.redline.RedlineUpdater.Editor;
using UnityEditor;
using UnityEngine;

namespace dev.runaxr.redline.Redline.Scripts.Editor {
    public abstract class RedlineHelp {
        [MenuItem("Redline/Help/Github", false, 1049)]
        public static void OpenDiscordLink() {
            Application.OpenURL("https://github.com/Redline/Redline");
        }

        [MenuItem("Redline/Update Importer Config", false, 1000)]
        public static void ForceUpdateConfigs() {
            RedlineImportManager.UpdateConfig();
        }

        public static void UpdateRedlineBtn() {
            RedlineAutomaticUpdateAndInstall.AutomaticRedlineInstaller();
        }
    }
}