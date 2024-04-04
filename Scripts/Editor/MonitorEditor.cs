using UnityEditor;
using UnityEngine;

namespace teleport
{
    [CustomEditor(typeof(teleport.Monitor))]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class MonitorEditor : Editor
	{
		bool background = true;
		bool dynamic_lighting=true;
		bool player_setup = true;
		public override void OnInspectorGUI()
        {
       //     base.OnInspectorGUI();
			teleport.Monitor monitor = (teleport.Monitor)target;
			
			background = EditorGUILayout.BeginFoldoutHeaderGroup(background, "Options");
			if (background)
            {
                monitor.backgroundMode=(BackgroundMode)EditorGUILayout.EnumPopup("Background Mode",monitor.backgroundMode);
				if(monitor.backgroundMode==BackgroundMode.COLOUR)
					monitor.BackgroundColour = EditorGUILayout.ColorField("Colour", monitor.BackgroundColour);
				monitor.lightingMode = (LightingMode)EditorGUILayout.EnumPopup("Dynamic Lighting", monitor.lightingMode);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			//	GUILayout.Space(10);= tag.coreData
			dynamic_lighting = EditorGUILayout.BeginFoldoutHeaderGroup(dynamic_lighting,"Lighting dynamic objects");
			if (dynamic_lighting)
            {
				monitor.environmentCubemap = (Cubemap)EditorGUILayout.ObjectField(new GUIContent("Source Environment Cubemap", "The environment texture to use when generating specular and diffuse cubemaps below."), monitor.environmentCubemap, typeof(Cubemap), false);
				monitor.environmentRenderTexture = (RenderTexture)EditorGUILayout.ObjectField(new GUIContent("Environment RenderTexture", "A rendertexture that will be generated from the Source Environment Cubemap above, " +
																										"this texture will be used for background in BackgroundMode.TEXTURE."), monitor.environmentRenderTexture, typeof(RenderTexture), false);
				monitor.specularRenderTexture = (RenderTexture)EditorGUILayout.ObjectField(new GUIContent("Specular Cubemap RenderTexture","A rendertexture that will be generated from the Source Environment Cubemap above, " +
																										"this texture will be used for specular lighting of movable objects."), monitor.specularRenderTexture, typeof(RenderTexture),false);
				monitor.diffuseRenderTexture = (RenderTexture)EditorGUILayout.ObjectField(new GUIContent("Diffuse Cubemap RenderTexture", "A rendertexture that will be generated from the Source Environment Cubemap above, " +
																										"this texture will be used for diffuse lighting of movable objects."), monitor.diffuseRenderTexture, typeof(RenderTexture), false);
				monitor.specularMultiplier=EditorGUILayout.FloatField("Multiplier", monitor.specularMultiplier);
				monitor.envMapSize = EditorGUILayout.IntField("Generate Cubemap Size", monitor.envMapSize);
				if (GUILayout.Button("Generate Env Maps"))
				{
					if (monitor && monitor.environmentCubemap)
					{
						monitor.generateEnvMaps = true;
					}
					else
					{

						EditorUtility.DisplayDialog("No environement cubemap ",
				   "Make sure you select an environment cubmap.", "OK");
					};
				}
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			GUILayout.Space(10);
			player_setup = EditorGUILayout.BeginFoldoutHeaderGroup(player_setup, "Players");
			if(player_setup)
			{ 
				monitor.defaultPlayerPrefab= (GameObject)EditorGUILayout.ObjectField("Default Player Prefab", monitor.defaultPlayerPrefab, typeof(GameObject), false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			GUILayout.Space(10);
            if(GUILayout.Button("Open Resource Window"))
                teleport.ResourceWindow.OpenResourceWindow();
        }
    }
}