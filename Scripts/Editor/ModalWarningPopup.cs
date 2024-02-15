using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace teleport
{ 
	public class ModalWarningPopup : EditorWindow
	{
		static public void Init()
		{
			EditorWindow mainWindow=EditorWindow.GetWindow(typeof(EditorWindow));
			ModalWarningPopup window = ScriptableObject.CreateInstance<ModalWarningPopup>();
			window.position = new Rect(mainWindow.position.x+ mainWindow.minSize.x / 2, mainWindow.position.y+ mainWindow.minSize.y / 2, 250, 150);
			window.ShowPopup();
		}

		void OnGUI()
		{
			EditorGUILayout.LabelField("This is an example of EditorWindow.ShowPopup", EditorStyles.wordWrappedLabel);
			GUILayout.Space(70);
			if (GUILayout.Button("OK"))
				this.Close();
		}
	}
}