using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using NUnit.Framework;

namespace teleport
{
    public class InputDefinitionsWindow : EditorWindow
	{
		[MenuItem("Teleport VR/Inputs")]
		public static void OpenInputDefinitionsWindow()
		{
			InputDefinitionsWindow window = GetWindow<InputDefinitionsWindow>(false, "Teleport Inputs");
			window.minSize = new Vector2(400, 200);
			window.Show();
		}
		static private Texture2D MakeTex(int width, int height, Color col)
		{
			Color[] pix = new Color[width * height];

			for (int i = 0; i < pix.Length; i++)
				pix[i] = col;

			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();

			return result;
		}
		Texture2D red4= null;
		Texture2D red7 = null;
		Texture2D green4 = null;
		Texture2D green7 = null;

		private void OnGUI()
        {
			if (red4 == null)
			{
				red4 = MakeTex(1, 1, new Color(1.0f, 0.0f, 0.0f, 0.4f));
				red7 = MakeTex(1, 1, new Color(1.0f, 0.0f, 0.0f, 0.7f));
				green4 = MakeTex(1, 1, new Color(0.0f, 1.0f, 0.0f, 0.4f));
				green7 = MakeTex(1, 1, new Color(0.0f, 1.0f, 0.0f, 0.7f));
			}
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			GUIStyle xstyle=new GUIStyle("Button");
			xstyle.normal.background =red4;
			xstyle.hover.background = red7;

			GUIStyle greenbutton = new GUIStyle("Button");
			greenbutton.normal.background = green4;
			greenbutton.hover.background = green7;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Input Definitions");
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Name", GUILayout.Width(120));
			EditorGUILayout.LabelField("Type", GUILayout.Width(100));
			EditorGUILayout.LabelField("Path", GUILayout.MinWidth(100));
			EditorGUILayout.LabelField("Path", GUILayout.MinWidth(100));
			EditorGUILayout.LabelField("Path", GUILayout.MinWidth(100));
			EditorGUILayout.LabelField("Path", GUILayout.MinWidth(100));
			EditorGUILayout.LabelField(" ", GUILayout.Width(20));
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginVertical();
			for (int i = 0; i < teleportSettings.inputDefinitions.Count; i++)
            {
				var def= teleportSettings.inputDefinitions[i];
				EditorGUILayout.BeginHorizontal();
				def.name=EditorGUILayout.TextField(def.name, GUILayout.Width(120));
				def.inputType=(avs.InputType)EditorGUILayout.EnumPopup(def.inputType, GUILayout.Width(100));

				List<string> paths=def.controlPath.Split(';').ToList();
				while(paths.Count<4)
					paths.Add("");
				paths[0]=EditorGUILayout.TextField(paths[0], GUILayout.MinWidth(100));
				paths[1] = EditorGUILayout.TextField(paths[1], GUILayout.MinWidth(100));
				paths[2] = EditorGUILayout.TextField(paths[2], GUILayout.MinWidth(100));
				paths[3] = EditorGUILayout.TextField(paths[3], GUILayout.MinWidth(100));
				paths = paths.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
				def.controlPath=string.Join(';',paths.ToArray());
				if (GUILayout.Button("x",xstyle, GUILayout.Width(20)))
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
			if (GUILayout.Button("+", greenbutton,GUILayout.Width(20)))
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