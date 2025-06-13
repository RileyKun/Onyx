using UnityEditor;
using UnityEngine;

namespace Redline.Editor {
    public abstract class RedlineHelp {
        [MenuItem("Redline/Help/Github", false, 1049)]
        public static void OpenDiscordLink() {
            Application.OpenURL("https://github.com/Redline-Team");
        }
    }
}