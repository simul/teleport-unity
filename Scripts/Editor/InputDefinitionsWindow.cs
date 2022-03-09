using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace teleport
{
    public class InputDefinitionsWindow : EditorWindow
	{
		[MenuItem("Teleport VR/Inputs")]
		public static void OpenInputDefinitionsWindow()
		{
			InputDefinitionsWindow window = GetWindow<InputDefinitionsWindow>(false, "Teleport Inputs");
			window.minSize = new Vector2(600, 200);
			window.Show();
		}
		private void OnGUI()
        {
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Input Definitions");
			EditorGUILayout.BeginVertical();
			for (int i = 0; i < teleportSettings.inputDefinitions.Count; i++)
            {
				var def= teleportSettings.inputDefinitions[i];
				EditorGUILayout.BeginHorizontal();
				def.name=EditorGUILayout.TextField(def.name);
				def.inputType=(avs.InputType)EditorGUILayout.EnumPopup(def.inputType);
				def.controlPath=EditorGUILayout.TextField(def.controlPath);
				if (GUILayout.Button("x"))
                {
					teleportSettings.inputDefinitions.RemoveAt(i);
					i--;
				}
				EditorGUILayout.EndHorizontal();

			}
			EditorGUILayout.EndVertical();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space();
			EditorGUILayout.Space();
			EditorGUILayout.Space();
			if (GUILayout.Button("+"))
			{
				var def = new InputDefinition();
				def.name = "New Input";
				def.inputType = avs.InputType.FloatEvent;
				def.controlPath = "//";
				teleportSettings.inputDefinitions.Add(def);
			}
			EditorGUILayout.EndHorizontal();
			// Force it to save:
			EditorUtility.SetDirty(teleportSettings);
		}
    }
}