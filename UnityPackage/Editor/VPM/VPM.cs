using UnityEditor;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Main entry point for VPM (VRChat Package Manager) functionality
    /// </summary>
    public static class VPM
    {
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