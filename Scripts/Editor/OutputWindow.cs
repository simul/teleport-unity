using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace teleport
{
	public class OutputWindow : EditorWindow
	{
		static OutputWindow windowInstance = null;
		[MenuItem("Teleport VR/Server Output Log", false, 2001)]
		public static void OpenOutputWindow()
		{
			OutputWindow window = GetWindow<OutputWindow>(false, "Teleport Server Output Log");
			window.minSize = new Vector2(200, 200);
			window.Show();
			windowInstance = window;
		}
		static public OutputWindow GetOutputWindow()
		{
			return windowInstance;
		}
		private Vector2 scroll;
		private GUIStyle scrollwindowStyle = new GUIStyle();
		private GUIStyle richText;
		string output="";
		public void MessageHandler(avs.LogSeverity Severity, string Msg, in System.IntPtr userData)
		{
			if (output.Length > 6000)
			{
				int pos = output.IndexOf("\n<color");
				output = output.Substring(pos + 1, output.Length - pos - 1);
			}
			switch (Severity)
			{
				case avs.LogSeverity.Warning:
					output += "<color=yellow>";
					break;
				case avs.LogSeverity.Error:
					output += "<color=#ff8888ff>";
					break;
				default:
					break;
			}
			output += Msg;
			switch (Severity)
			{
				case avs.LogSeverity.Error:
				case avs.LogSeverity.Warning:
					output += "</color>";
					break;
				default:
					break;
			}
		}
		private void OnGUI()
		{
			if(Monitor.editorMessageHandler==null)
				Monitor.editorMessageHandler=MessageHandler;
			if (richText==null)
			{	
				richText = new GUIStyle(GUI.skin.textArea);
				richText.normal.textColor = Color.white;
				richText.richText = true;
			}
			scrollwindowStyle = GUI.skin.box;
			if (GUILayout.Button("Clear"))
            {
				output="";
            }
			scroll = EditorGUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, scrollwindowStyle);
			EditorGUILayout.TextArea(output, richText, GUILayout.ExpandHeight(true));// GUILayout.Height(position.height - 30));
			EditorGUILayout.EndScrollView();
		}
	}
}