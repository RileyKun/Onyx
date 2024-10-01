using System;
using UnityEditor;

namespace Redline.Editor
{
	public static class IMGUIDebugger
	{

		static Type type = Type.GetType("UnityEditor.GUIViewDebuggerWindow,UnityEditor");

		[MenuItem("Redline/IMGUI Debugger")]
		public static void Open() => EditorWindow.GetWindow(type).Show();
	
	
	}
}