using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
    public abstract class RedlineHelp {
        [MenuItem("Redline/Help/Github", false, 1049)]
        public static void OpenDiscordLink() {
            Application.OpenURL("https://github.com/Redline/Redline");
        }

        [MenuItem("Redline/Update Importer Config", false, 1000)]
        public static void ForceUpdateConfigs() {
            RedlineImportManager.UpdateConfig();
        }
    }
}