using UnityEditor;
using UnityEngine;

namespace Redline.Scripts.Editor {
    public abstract class RedlineHelp {
        // Menu Item to open GitHub link
        [MenuItem("Redline/Help/Github", false, 1049)]
        public static void OpenGithubLink() {
            Application.OpenURL("https://github.com/Redline/Redline");
        }

        // Menu Item to force update the importer config
        [MenuItem("Redline/Update Importer Config", false, 1000)]
        public static void ForceUpdateConfigs() {
            try {
                bool isUpdated = RedlineImportManager.UpdateConfig();
                if (isUpdated) {
                    Debug.Log("Redline config successfully updated.");
                } else {
                    Debug.LogWarning("No updates were necessary for the config.");
                }
            } catch (System.Exception ex) {
                Debug.LogError("Failed to update Redline config: " + ex.Message);
                EditorUtility.DisplayDialog("Update Error", "Failed to update Redline config: " + ex.Message, "OK");
            }
        }
    }
}
