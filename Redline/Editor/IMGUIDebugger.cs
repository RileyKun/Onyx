using System;
using UnityEditor;

namespace Redline.Editor
{
    public static class IMGUIDebugger
    {
        // Type to represent UnityEditor.GUIViewDebuggerWindow
        private static readonly Type Type = Type.GetType("UnityEditor.GUIViewDebuggerWindow,UnityEditor");

        // Open the IMGUI Debugger Window via the Redline menu
        [MenuItem("Redline/IMGUI Debugger")]
        public static void Open()
        {
            // If the Type was found, open the corresponding window
            if (Type != null)
            {
                EditorWindow.GetWindow(Type).Show();
            }
            else
            {
                // Log an error if the type could not be found
                EditorUtility.DisplayDialog("Error", "IMGUI Debugger Window is not available.", "OK");
            }
        }

        // Optional: Add validation to disable the menu item if IMGUI Debugger is not available
        [MenuItem("Redline/IMGUI Debugger", true)]
        private static bool ValidateIMGUIWindow()
        {
            // Check if the type is available; this will disable the menu item if not.
            return Type != null;
        }
    }
}
