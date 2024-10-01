using System;
using UnityEditor;

namespace Redline.Editor
{
	public static class IMGUIDebugger
	{
		private static readonly Type Type = Type.GetType("UnityEditor.GUIViewDebuggerWindow,UnityEditor");

		[MenuItem("Redline/IMGUI Debugger")]
		public static void Open() => EditorWindow.GetWindow(Type).Show();
	
	
	}
}