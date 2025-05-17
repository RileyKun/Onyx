using UnityEditor;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Main entry point for VPM (VRChat Package Manager) functionality
    /// </summary>
    public static class VPM
    {
        [MenuItem("Redline/Open VPM Manager")]
        public static void OpenVPMManager()
        {
            VPMWindow.ShowWindow();
        }

        /// <summary>
        /// Initializes the VPM system
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Initialize the VPM manager when Unity starts
            VPMManager.Initialize();
        }
    }
}