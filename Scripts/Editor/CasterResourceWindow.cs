﻿using System;
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
		enum ResourceWindowCategories {EXTRACTION, SETUP, DEBUG};

		//References to assets.
		private GeometrySource geometrySource;
		private ComputeShader textureShader;
		private TeleportSettings teleportSettings;

		//List of data that was extracted in the last extraction operation.
		private List<GameObject> streamableGameObjects = new List<GameObject>();
		private RenderTexture[] renderTextures = new RenderTexture[0];
		private bool includePlayerParts=true;
		//Text styles.
		private GUIStyle richText = new GUIStyle();
		private GUIStyle warningText = new GUIStyle();
		private GUIStyle errorText = new GUIStyle();

		//GUI variables that control user-changeable properties.
		private Vector2 scrollPosition_gameObjects;
		private Vector2 scrollPosition_textures;
		private bool foldout_gameObjects = false;
		private bool foldout_textures = false;

		private string[] categories;
		private int selectedCategory = 0;

		[MenuItem("Teleport VR/Resource Manager")]
		public static void OpenResourceWindow()
		{
			CasterResourceWindow window = GetWindow<CasterResourceWindow>(false, "TeleportVR Resource Manager");
			window.minSize = new Vector2(600, 200);
			window.Show();
		}

		private void Awake()
		{
			geometrySource = GeometrySource.GetGeometrySource();

			string shaderGUID = AssetDatabase.FindAssets("ExtractTextureData t:ComputeShader")[0];
			textureShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID));

			teleportSettings = TeleportSettings.GetOrCreateSettings();
		}

		//Use this to setup variables that are likely to change on a hot-reload.
		private void OnFocus()
		{
			richText.normal.textColor = Color.white;
			richText.richText = true;
			
			warningText.normal.textColor = new Color(1.0f, 1.0f, 0.0f);
			errorText.normal.textColor = Color.red;

			//Fill categories array with enumeration names.
			categories = Enum.GetNames(typeof(ResourceWindowCategories));
			//Change names to title-case; i.e. EXTRACTION -> Extraction.
			for(int i = 0; i < categories.Length; i++)
			{
				categories[i] = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(categories[i].ToLower());
			}
		}

		private void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();
			selectedCategory = GUILayout.SelectionGrid(selectedCategory, categories, 1, GUILayout.ExpandWidth(false));

			EditorGUILayout.BeginVertical();
			switch((ResourceWindowCategories)selectedCategory)
			{
				case ResourceWindowCategories.EXTRACTION:
					DrawExtractionLayout();
					break;
				case ResourceWindowCategories.SETUP:
					DrawSetupLayout();
					break;
				case ResourceWindowCategories.DEBUG:
					DrawDebugLayout();
					break;
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void DrawExtractionLayout()
		{
			includePlayerParts=GUILayout.Toggle(includePlayerParts,"Include Player Parts");
			if (GUILayout.Button("Find Scene Streamables"))
			{
				FindSceneStreamables();
			}

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Extract Selected Geometry"))
			{
				ExtractSelectedGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES);
			}

			if(GUILayout.Button("Force Selected Geometry Extraction"))
			{
				ExtractSelectedGeometry(GeometrySource.ForceExtractionMask.FORCE_EVERYTHING);
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Extract Scene Geometry"))
			{
				ExtractSceneGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
			}

			if(GUILayout.Button("Force Scene Geometry Extraction"))
			{
				ExtractSceneGeometry(GeometrySource.ForceExtractionMask.FORCE_EVERYTHING);
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Extract Project Geometry"))
			{
				ExtractProjectGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
			}

			if(GUILayout.Button("Force Project Geometry Extraction"))
			{
				ExtractProjectGeometry(GeometrySource.ForceExtractionMask.FORCE_EVERYTHING);
			}

			EditorGUILayout.EndHorizontal();

			foldout_gameObjects = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_gameObjects, "Streamable Candidate GameObjects (" + streamableGameObjects.Count + ")");
			if(foldout_gameObjects)
			{
				scrollPosition_gameObjects = EditorGUILayout.BeginScrollView(scrollPosition_gameObjects);

				foreach(GameObject gameObject in streamableGameObjects)
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

			foldout_textures = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_textures, "Last Extracted Textures (" + renderTextures.Length + ")");
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
		}

		private void DrawSetupLayout()
		{
			//Names of the loaded scenes from the SceneManager.
			string loadedScenes = "";
			for(int i = 0; i < SceneManager.sceneCount; i++)
			{
				loadedScenes += SceneManager.GetSceneAt(i).name;

				//Include separator if this isn't the last index.
				if(i != SceneManager.sceneCount - 1)
				{
					loadedScenes += ", ";
				}
			}
			//Display list of all loaded scenes.
			EditorGUILayout.LabelField($"Loaded Scenes: {loadedScenes}");

			//Only display following GUI when the tag is set.
			if(teleportSettings.TagToStream.Length > 0)
			{
				GameObject[] gameObjectsTagged = GameObject.FindGameObjectsWithTag(teleportSettings.TagToStream);

				EditorGUILayout.LabelField($"There are <b>{gameObjectsTagged.Length}</b> GameObjects with the '{teleportSettings.TagToStream}' tag.", richText);

				foreach(GameObject gameObject in gameObjectsTagged)
				{
					Collider[] colliders = gameObject.GetComponents<Collider>();
					if(colliders.Length > 1)
					{
						EditorGUILayout.LabelField("Warning: " + gameObject.name + " has " + colliders.Length + " collision components.", warningText);
					}
				}

				if(GUILayout.Button($"Clear tag '{teleportSettings.TagToStream}' from all GameObjects."))
				{
					foreach(var gameObject in gameObjectsTagged)
					{
						gameObject.tag = "Untagged";
					}
				}

				EditorGUILayout.LabelField("Applies the tag to objects with collision.");
				if(GUILayout.Button($"Apply {teleportSettings.TagToStream} tag to selected GameObjects."))
				{
					foreach(var gameObject in UnityEditor.Selection.gameObjects)
					{
						var collider = gameObject.GetComponent<Collider>();
						var mesh = gameObject.GetComponentsInChildren<MeshFilter>();
						if(collider != null && mesh.Length > 0 && gameObject.tag == "Untagged")
						{
							gameObject.tag = teleportSettings.TagToStream;
						}
					}
					EditorMask.Initialize();
				}
			}

			EditorGUILayout.LabelField("Adds a trigger box collider to selected GameObjects with a MeshFilter, but without a collider.");
			if(GUILayout.Button("Add box collider to selected GameObjects."))
			{
				foreach(var gameObject in Selection.gameObjects)
				{
					var collider = gameObject.GetComponent<Collider>();
					var meshFilter = gameObject.GetComponent<MeshFilter>();
					if(meshFilter != null && collider == null)
					{
						BoxCollider bc = gameObject.AddComponent<BoxCollider>();
						bc.isTrigger = true;
					}
				}
			}

			EditorGUILayout.LabelField("Recursively adds box collision and tag to selected and children where appropriate.");
			if(GUILayout.Button("Setup selected and children for streaming."))
			{
				foreach(var gameObject in Selection.gameObjects)
				{
					SetupGameObjectAndChildrenForStreaming(gameObject);
				}
			}
		}

		private void DrawDebugLayout()
		{
			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Clear Cached Data"))
			{
				geometrySource.ClearData();
				streamableGameObjects = new List<GameObject>();

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

			EditorGUILayout.EndHorizontal();

			if(GUILayout.Button("Reload Geometry From Disk"))
			{
				geometrySource.LoadFromDisk();
			}
		}

		private void FindSceneStreamables()
		{
			streamableGameObjects = geometrySource.GetStreamableObjects(includePlayerParts);
		}

		private void ExtractGeometry(List<GameObject> extractionList, GeometrySource.ForceExtractionMask forceMask)
		{
			for(int i = 0; i < extractionList.Count; i++)
			{
				GameObject gameObject = extractionList[i];

				if(EditorUtility.DisplayCancelableProgressBar($"Extracting Geometry ({i + 1} / {extractionList.Count})", $"Processing \"{gameObject.name}\".", (float)(i + 1) / extractionList.Count))
				{
					return;
				}

				geometrySource.AddNode(gameObject, forceMask);
			}
			ExtractTextures();

			geometrySource.SaveToDisk();
		}

		private void ExtractSelectedGeometry(GeometrySource.ForceExtractionMask forceMask)
		{
			streamableGameObjects = new List<GameObject>(Selection.gameObjects);
			ExtractGeometry(streamableGameObjects, forceMask);
		}

		private void ExtractSceneGeometry(GeometrySource.ForceExtractionMask forceMask)
		{
			streamableGameObjects = geometrySource.GetStreamableObjects(includePlayerParts);
			ExtractGeometry(streamableGameObjects, forceMask);
		}

		private void ExtractProjectGeometry(GeometrySource.ForceExtractionMask forceMask)
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

				ExtractSceneGeometry(forceMask);
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

				if(EditorUtility.DisplayCancelableProgressBar($"Extracting Textures ({i + 1} / {renderTextures.Length})", $"Processing \"{sourceTexture.name}\".", (float)(i + 1) / renderTextures.Length))
				{
					return;
				}

				//If we always created a new render texture, then reloading would lose the link to the render texture in the inspector.
				if(renderTextures[i] == null)
				{
					renderTextures[i] = new RenderTexture(sourceTexture.width, sourceTexture.height, 0);
				}
				else
				{
					renderTextures[i].Release();
					renderTextures[i].width = sourceTexture.width;
					renderTextures[i].height = sourceTexture.height;
					renderTextures[i].depth = 0;
				}

				renderTextures[i].enableRandomWrite = true;
				renderTextures[i].name = $"{geometrySource.texturesWaitingForExtraction[i].unityTexture.name} ({geometrySource.texturesWaitingForExtraction[i].id})";
				renderTextures[i].Create();

				//Normal maps need to be extracted differently; i.e. convert from DXT5nm format.
				string path = AssetDatabase.GetAssetPath(sourceTexture);
				TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
				TextureImporterType textureType = textureImporter ? textureImporter.textureType : TextureImporterType.Default;

				string shaderName = textureType == UnityEditor.TextureImporterType.NormalMap ? "ExtractNormalMap" : "ExtractTexture";
				shaderName += UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma ? "Gamma" : "Linear";
				
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
			geometrySource.texturesWaitingForExtraction.Clear();
		}

		private void SetupGameObjectAndChildrenForStreaming(GameObject o)
		{
			foreach(var mf in o.GetComponentsInChildren<MeshFilter>())
			{
				if(mf.gameObject.GetComponent<Renderer>() == null || !mf.gameObject.GetComponent<Renderer>().isVisible)
				{
					continue;
				}

				var go = mf.gameObject;
				if(go.GetComponent<Collider>() != null)
				{
					go.tag = teleportSettings.TagToStream;
					continue;
				}

				// Will use a parent's collider if it has a parent with a mesh filter.
				// Otherwise a box collision will be added.
				if(go.GetComponentsInParent<MeshFilter>().Length ==
					go.GetComponents<MeshFilter>().Length)
				{
					go.tag = teleportSettings.TagToStream;
					BoxCollider bc = go.AddComponent<BoxCollider>();
					bc.isTrigger = true;
				}
			}
		}

	}
}
