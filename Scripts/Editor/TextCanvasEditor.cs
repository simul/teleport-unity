using UnityEditor;
using UnityEngine;

namespace teleport
{
    [CustomEditor(typeof(teleport.TextCanvas))]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class TextCanvasEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
		/*	teleport.TextCanvas textCanvas = (teleport.TextCanvas)target;
			textCanvas.width=EditorGUILayout.FloatField(textCanvas.width);
			textCanvas.height=EditorGUILayout.FloatField(textCanvas.height);
			textCanvas.font=(Font)EditorGUILayout.ObjectField((Object)textCanvas.font,typeof(Font));
			GUILayout.Space(10);
            if(GUILayout.Button("Open Resource Window"))
                teleport.ResourceWindow.OpenResourceWindow();*/
        }
    }
}