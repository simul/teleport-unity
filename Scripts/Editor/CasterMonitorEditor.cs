﻿using UnityEditor;
using UnityEngine;

namespace teleport
{
    [CustomEditor(typeof(teleport.Monitor))]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class CasterMonitorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
			teleport.Monitor monitor = (teleport.Monitor)target;
			GUILayout.Space(10);
			if (GUILayout.Button("Generate Env Maps"))
			{
				if(monitor&&monitor.environmentCubemap)
				{
					monitor.generateEnvMaps=true;
				}
			}

			GUILayout.Space(10);
            if(GUILayout.Button("Open Resource Window"))
                teleport.ResourceWindow.OpenResourceWindow();
        }
    }
}