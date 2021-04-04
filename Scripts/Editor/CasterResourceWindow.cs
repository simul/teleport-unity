using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace teleport
{
	public class CasterResourceWindow : EditorWindow
	{
		//References to assets.
		private GeometrySource geometrySource;
		private ComputeShader textureShader;
		private TeleportSettings teleportSettings;

		//List of found/extracted data.
		private List<GameObject> streamedSceneObjects = new List<GameObject>(); //Game objects that were found to be valid streamables in the opened scenes.
		private RenderTexture[] renderTextures = new RenderTexture[0];

		//GUI variables that control user-changeable properties.
		private Vector2 scrollPosition_gameObjects;
		private Vector2 scrollPosition_textures;
		private bool foldout_gameObjects = false;
		private bool foldout_textures = false;
		static GUIStyle redStyle =new GUIStyle();

		[MenuItem("Teleport VR/Resource Manager")]
		public static void OpenResourceWindow()
		{
			redStyle.normal.textColor = Color.red;
			CasterResourceWindow window = GetWindow<CasterResourceWindow>(false, "TeleportVR Resource Manager");
			window.minSize = new Vector2(600, 200);
			window.Show();
		}

		private void Awake()
		{
			redStyle.normal.textColor = Color.red;
			geometrySource = GeometrySource.GetGeometrySource();

			string shaderGUID = AssetDatabase.FindAssets("ExtractTextureData t:ComputeShader")[0];
			textureShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID));

			teleportSettings = TeleportSettings.GetOrCreateSettings();
		}

        private void OnGUI()
		{
			redStyle.normal.textColor = Color.red;
			foldout_gameObjects = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_gameObjects, "GameObjects (" + streamedSceneObjects.Count + ")");
			if(foldout_gameObjects)
			{
				scrollPosition_gameObjects = EditorGUILayout.BeginScrollView(scrollPosition_gameObjects);

				foreach(GameObject gameObject in streamedSceneObjects)
				{
					EditorGUILayout.BeginHorizontal();

					using(new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.ObjectField(gameObject, typeof(GameObject), true);
					}

					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.EndScrollView();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			foldout_textures = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_textures, "Textures (" + renderTextures.Length + ")");
			if(foldout_textures)
			{
				scrollPosition_textures = EditorGUILayout.BeginScrollView(scrollPosition_textures);

				foreach(RenderTexture renderTexture in renderTextures)
				{
					if(!renderTexture) break;

					EditorGUILayout.BeginHorizontal();

					using(new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.ObjectField(renderTexture, typeof(RenderTexture), true);
					}

					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.EndScrollView();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Find Scene Streamables"))
			{
				FindSceneStreamables();
			}

			if(GUILayout.Button("Extract Scene Geometry"))
			{
				ExtractSceneGeometry();
			}

			if(GUILayout.Button("Extract Project Geometry"))
			{
				ExtractProjectGeometry();
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.LabelField("Debug Controls");

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Clear Cached Data"))
			{
				geometrySource.ClearData();
				streamedSceneObjects = new List<GameObject>();

				foreach(RenderTexture texture in renderTextures)
				{
					if(texture)
					{
						texture.Release();
					}
				}
				renderTextures = new RenderTexture[0];
			}

			//Create a new file explorer window, displaying the folder where compressed textures are saved.
			if(GUILayout.Button("Open Compressed Texture Folder"))
			{
				//File explorer will treat paths with forward slashes as invalid.
				System.Diagnostics.Process.Start("explorer.exe", geometrySource.compressedTexturesFolderPath.Replace('/', '\\'));
			}

			if(GUILayout.Button("Force Extract"))
			{
				geometrySource.ClearData();
				ExtractSceneGeometry();
			}

			EditorGUILayout.EndHorizontal();

			if(GUILayout.Button("Reload From Disk"))
			{
				geometrySource.LoadFromDisk();
			}
			EditorGUILayout.LabelField("Scene: ");

			string[] options = new string[SceneManager.sceneCount];
			for (int n = 0; n < SceneManager.sceneCount; ++n)
			{
				options[n]= SceneManager.GetSceneAt(n).name;
			}
			/*
			sceneIndex = EditorGUILayout.Popup(sceneIndex, options);
			Scene scene = SceneManager.GetActiveScene();
			if(sceneIndex<SceneManager.sceneCount)
				scene= SceneManager.GetSceneAt(sceneIndex);*/
			if(teleportSettings.TagToStream.Length>0)
			{ 
				GameObject[] gameObjectsTagged = GameObject.FindGameObjectsWithTag(teleportSettings.TagToStream);
				EditorGUILayout.LabelField(gameObjectsTagged.Length+" with tag ");
				if (GUILayout.Button("Clear Tag "+ teleportSettings.TagToStream+" from all"))
				{
					foreach(var gameObject in gameObjectsTagged)
					{
						gameObject.tag="Untagged";
					}
				}
				if (GUILayout.Button("Add Collision to selected"))
				{
					foreach (var gameObject in UnityEditor.Selection.gameObjects)
					{
						var collider = gameObject.GetComponent<Collider>();
						var mesh = gameObject.GetComponent<MeshFilter>();
						if (mesh != null && collider==null)
						{
							BoxCollider bc=gameObject.AddComponent<BoxCollider>();
							bc.isTrigger=true;
						}
					}
				}
				EditorGUILayout.LabelField("Adds trigger box collision where collision is not already present.");
				if (GUILayout.Button("Apply Tag " + teleportSettings.TagToStream + " to selected"))
				{
					foreach(var gameObject in UnityEditor.Selection.gameObjects)
					{
						var collider=gameObject.GetComponent<Collider>();
						var mesh=gameObject.GetComponentsInChildren<MeshFilter>();
						if(collider!=null&&mesh.Length>0&&gameObject.tag=="Untagged")
						{
							gameObject.tag= teleportSettings.TagToStream;
						}
					}
					EditorMask.Initialize();
				}
				EditorGUILayout.LabelField("Applies the tag to objects with collision.");
				foreach(var o in gameObjectsTagged)
				{
					var collisions=o.GetComponents<Collider>();
					if(collisions.Length>1)
					{
						EditorGUILayout.LabelField("Warning: "+o.name+" has "+collisions.Length+" collision components.", redStyle);
					}
				}
			}
		}
		int sceneIndex=0;
		private void FindSceneStreamables()
		{
			streamedSceneObjects = geometrySource.GetStreamableObjects();
		}

		private void ExtractSceneGeometry()
		{
			FindSceneStreamables();
			for(int i = 0; i < streamedSceneObjects.Count; i++)
			{
				GameObject gameObject = streamedSceneObjects[i];

				if(EditorUtility.DisplayCancelableProgressBar("Extracting Scene Geometry", "Processing " + gameObject.name, i / streamedSceneObjects.Count))
					return;
				geometrySource.AddNode(gameObject, true);
			}
			ExtractTextures();

			geometrySource.SaveToDisk();
		}

		private void ExtractProjectGeometry()
        {
			string[] originalScenes = new string[SceneManager.sceneCount];
			//Store scenes that were originally open.
			for(int i = 0; i < SceneManager.sceneCount; i++)
			{
				originalScenes[i] = SceneManager.GetSceneAt(i).path;
			}

			//Open each scene in the build settings and extract the data from them.
			foreach(EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
			{
				EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);

				ExtractSceneGeometry();
			}

			//Re-open scenes that were originally open.
			EditorSceneManager.OpenScene(originalScenes[0]);
			for(int i = 1; i < originalScenes.Length; i++)
			{
				EditorSceneManager.OpenScene(originalScenes[i], OpenSceneMode.Additive);
			}
		}

		private void ExtractTextures()
		{
			//According to the Unity docs, we need to call Release() on any render textures we are done with.
			for(int i = geometrySource.texturesWaitingForExtraction.Count; i < renderTextures.Length; i++)
			{
				if (renderTextures[i])
				{
					renderTextures[i].Release();
				}
			}
			//Resize the array, instead of simply creating a new one, as we want to keep the same render textures for quicker debugging.
			Array.Resize(ref renderTextures, geometrySource.texturesWaitingForExtraction.Count);

			//Extract texture data with a compute shader, rip the data, and store in the native C++ plugin.
			for(int i = 0; i < renderTextures.Length; i++)
			{
				Texture2D sourceTexture = (Texture2D)geometrySource.texturesWaitingForExtraction[i].unityTexture;

				if (EditorUtility.DisplayCancelableProgressBar("Extracting Textures", "Processing " + sourceTexture.name, i / renderTextures.Length))
					return;
				//If we always created a new render texture, then reloading would lose the link to the render texture in the inspector.
				if (renderTextures[i] == null)
					renderTextures[i] = new RenderTexture(sourceTexture.width, sourceTexture.height, 0);
				else
				{
					renderTextures[i].Release();
					renderTextures[i].width = sourceTexture.width;
					renderTextures[i].height = sourceTexture.height;
					renderTextures[i].depth = 0;
				}

				renderTextures[i].enableRandomWrite = true;
				renderTextures[i].name = $"{geometrySource.texturesWaitingForExtraction[i].unityTexture.name} ({geometrySource.texturesWaitingForExtraction[i].id.ToString()})";
				renderTextures[i].Create();

				//Normal maps need to be extracted differently; i.e. convert from DXT5nm format.
				string path = AssetDatabase.GetAssetPath(sourceTexture);
				TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
				TextureImporterType textureType = textureImporter ? textureImporter.textureType : TextureImporterType.Default;

				string shaderName="";
				if(textureType == UnityEditor.TextureImporterType.NormalMap)
					shaderName= "ExtractNormalMap";
				else
					shaderName = "ExtractTexture";
				if(UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma)
					shaderName+="Gamma";
				else
					shaderName+="Linear";

				int kernelHandle = textureShader.FindKernel(shaderName);

				textureShader.SetTexture(kernelHandle, "Source", sourceTexture);
				textureShader.SetTexture(kernelHandle, "Result", renderTextures[i]);
				textureShader.Dispatch(kernelHandle, sourceTexture.width / 8, sourceTexture.height / 8, 1);

				//Rip data from render texture, and store in GeometryStore.
				{
					//Read pixel data into Texture2D, so that it can be read.
					Texture2D readTexture = new Texture2D(renderTextures[i].width, renderTextures[i].height, renderTextures[i].graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
					{
						RenderTexture oldActive = RenderTexture.active;

						RenderTexture.active = renderTextures[i];
						readTexture.ReadPixels(new Rect(0, 0, renderTextures[i].width, renderTextures[i].height), 0, 0);
						readTexture.Apply();

						RenderTexture.active = oldActive;
					}
					Color32[] pixelData = readTexture.GetPixels32();

					avs.Texture textureData = geometrySource.texturesWaitingForExtraction[i].textureData;
					textureData.format = avs.TextureFormat.RGBA8;
					textureData.bytesPerPixel = 4;

					int byteSize = Marshal.SizeOf<byte>();
					textureData.dataSize = (uint)(pixelData.Length * 4 * byteSize);
					textureData.data = Marshal.AllocCoTaskMem((int)textureData.dataSize);

					int byteOffset = 0;
					foreach(Color32 pixel in pixelData)
					{
						Marshal.WriteByte(textureData.data, byteOffset, pixel.r);
						byteOffset += byteSize;

						Marshal.WriteByte(textureData.data, byteOffset, pixel.g);
						byteOffset += byteSize;

						Marshal.WriteByte(textureData.data, byteOffset, pixel.b);
						byteOffset += byteSize;

						Marshal.WriteByte(textureData.data, byteOffset, pixel.a);
						byteOffset += byteSize;
					}

					geometrySource.AddTextureData(sourceTexture, textureData);

					Marshal.FreeCoTaskMem(textureData.data);
				}
			}

			geometrySource.CompressTextures();
		}
	}
}
