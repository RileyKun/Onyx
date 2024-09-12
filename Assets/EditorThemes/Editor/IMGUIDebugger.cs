using System;
using UnityEditor;

namespace EditorThemes.Editor
{
	public static class IMGUIDebugger
	{
		private static readonly Type Type = Type.GetType("UnityEditor.GUIViewDebuggerWindow,UnityEditor");

		[MenuItem("HyperX/IMGUI Debugger")]
		public static void Open() => EditorWindow.GetWindow(Type).Show();
	
	
	}
}