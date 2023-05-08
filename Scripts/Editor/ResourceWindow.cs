﻿using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using uid = System.UInt64;

namespace teleport
{
	public class ResourceWindow : EditorWindow
	{
		public static bool SearchStringMatch(string source, string toCheck)
		{
			return source?.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		enum ResourceWindowCategories { SETUP, RESOURCES, SCENE_REFS, DEBUG};

		//References to assets.
		private GeometrySource geometrySource;
		private ComputeShader textureShader;
		private TeleportSettings teleportSettings;

		//List of data that was extracted in the last extraction operation.
		private RenderTexture[] renderTextures = new RenderTexture[0];
		private bool verifyGeometry = false;
		private bool forceExtraction=false;

		//Text styles.
		private GUIStyle richText ;
		private GUIStyle warningText ;
		private GUIStyle errorText ;
		private GUIStyle hScrollbarStyle;
		private GUIStyle vScrollbarStyle;
		private GUIStyle scrollwindowStyle;
		private GUIStyle labelText ;
		private GUIStyle titleStyle;

		//GUI variables that control user-changeable properties.
		private Vector2 scrollPosition_gameObjects;
		private Vector2 scrollPosition_textures;
	//	private bool foldout_gameObjects = false;
		private bool foldout_textures = false;
		private Vector2 scrollPosition_meshTable;

		private string[] categories;
		private int selectedCategory = 0;

		[MenuItem("Teleport VR/Resource Manager")]
		public static void OpenResourceWindow()
		{
			ResourceWindow window = GetWindow<ResourceWindow>(false, "Teleport Resources");
			window.minSize = new Vector2(600, 200);
			window.Show();
		}

		private void Awake()
		{
			labelText = null;
			geometrySource = GeometrySource.GetGeometrySource();

			string shaderGUID = AssetDatabase.FindAssets("ExtractTextureData t:ComputeShader")[0];
			textureShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID));

			teleportSettings = TeleportSettings.GetOrCreateSettings();
		}

		//Use this to setup variables that are likely to change on a hot-reload.
		private void OnFocus()
		{
			labelText=null;
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
			if (labelText==null)
			{
				richText = new GUIStyle(GUI.skin.textArea);
				warningText = new GUIStyle(GUI.skin.label);
				errorText = new GUIStyle(GUI.skin.label);
				hScrollbarStyle = new GUIStyle(GUI.skin.horizontalScrollbar);
				vScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
				labelText = new GUIStyle(GUI.skin.label);
				scrollwindowStyle = new GUIStyle(GUI.skin.box);
				titleStyle = new GUIStyle(GUI.skin.label);
				titleStyle.fontSize = (GUI.skin.label.fontSize * 5) / 4;
				titleStyle.fontStyle=FontStyle.Bold;
				richText.normal.textColor = Color.white;
				richText.richText = true;

				warningText.normal.textColor = new Color(1.0f, 1.0f, 0.0f);
				errorText.normal.textColor = Color.red;
			}
			DrawExtractionLayout();
			EditorGUILayout.Separator();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Advanced",titleStyle);
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginHorizontal();
			selectedCategory = GUILayout.SelectionGrid(selectedCategory, categories, 1, GUILayout.ExpandWidth(false));

			EditorGUILayout.BeginVertical();
			resourceSearchText = EditorGUILayout.TextField("Search ", resourceSearchText);
			switch ((ResourceWindowCategories)selectedCategory)
			{
				case ResourceWindowCategories.SETUP:
					DrawSetupLayout();
					break;
				case ResourceWindowCategories.RESOURCES:
					DrawResourcesLayout();
					break;
				case ResourceWindowCategories.SCENE_REFS:
					DrawSceneRefsLayout();
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
			EditorGUILayout.LabelField("Extraction", titleStyle);
			EditorGUILayout.Space(10);
			GUI.enabled = !Application.isPlaying;
			labelText.alignment=TextAnchor.MiddleRight;
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.BeginVertical();
			{
				UnityEngine.Object[] activeGOs =
					Selection.GetFiltered(
						typeof(GameObject),
						SelectionMode.Editable | SelectionMode.TopLevel);
				{ 
					bool wasEnabled=GUI.enabled;
					GUI.enabled = activeGOs.Length>0;
					//EditorGUILayout.disab
					EditorGUILayout.BeginHorizontal(); 
					GUILayout.Label("Selected Geometry:", labelText, GUILayout.Width(300));
					if (GUILayout.Button("Extract"))
					{
						ExtractSelectedGeometry(forceExtraction? GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES:GeometrySource.ForceExtractionMask.FORCE_NODES);
					}
					EditorGUILayout.EndHorizontal();
					GUI.enabled=wasEnabled;
				}
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Scene Geometry:", labelText, GUILayout.Width(300));
				if (GUILayout.Button("Extract"))
				{
					ExtractSceneGeometry(forceExtraction ? GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES : GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Project Geometry:", labelText, GUILayout.Width(300));
				if (GUILayout.Button("Extract "))
				{
					ExtractProjectGeometry(forceExtraction ? GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES : GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Global Illumination Textures:", labelText, GUILayout.Width(300));
				if (GUILayout.Button("Extract "))
				{
					ExtractGlobalIlluminationTextures(SceneManager.GetActiveScene());
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Dynamic Object Lighting Textures:", labelText, GUILayout.Width(300));
                bool wasEnabled2 = GUI.enabled;
                GUI.enabled &= forceExtraction||(Monitor.Instance?Monitor.Instance.envMapsGenerated:false);

                if (GUILayout.Button("Extract "))
				{
					ExtractDynamicObjectLightingTextures();
                }
                GUI.enabled = wasEnabled2;
                EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();


			EditorGUILayout.BeginVertical();
				verifyGeometry = GUILayout.Toggle(verifyGeometry, "Verify Compressed Geometry");
				forceExtraction = GUILayout.Toggle(forceExtraction, "Force Extraction");
			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical();

			foldout_textures = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_textures, "Last Extracted Textures (" + renderTextures.Length + ")");
			if (foldout_textures)
			{
				scrollPosition_textures = EditorGUILayout.BeginScrollView(scrollPosition_textures);

				foreach (RenderTexture renderTexture in renderTextures)
				{
					if (!renderTexture)
						break;

					EditorGUILayout.BeginHorizontal();

					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.ObjectField(renderTexture, typeof(RenderTexture), true);
					}

					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.EndScrollView();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(10);
			GUI.enabled=true;
		}
		private Vector2 scrollPosition_scenes;
		private Vector2 scrollPosition_sceneMeshes;
		private Vector2 scrollPosition_sceneResourcePaths;
		private Scene selected_scene;
		void DrawSceneRefsLayout()
		{
			SceneReferenceManager.GetSceneReferenceManager(SceneManager.GetActiveScene());
			var scenerefs=SceneReferenceManager.GetSceneReferenceManagers();
			SceneResourcePathManager.GetSceneResourcePathManager(SceneManager.GetActiveScene());
			var sceneres = SceneResourcePathManager.GetSceneResourcePathManagers();
			EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scene", GUILayout.Width(100));
            EditorGUILayout.LabelField("Resource Paths", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            //GUI.skin.scrollView ;
            //.normal.background = Color.white; 
            scrollPosition_scenes = EditorGUILayout.BeginScrollView(scrollPosition_scenes, false, true, hScrollbarStyle, vScrollbarStyle, scrollwindowStyle);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                SceneResourcePathManager p = null;
                sceneres.TryGetValue(s, out p);
                EditorGUILayout.BeginHorizontal();
                if (EditorGUILayout.LinkButton(s.name, GUILayout.Width(100)))
                {
                    selected_scene = s;
                }
                EditorGUILayout.LabelField(p ? p.GetResourcePathMap().Count.ToString() : "0", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Game Object");
			EditorGUILayout.LabelField("Mesh");
			EditorGUILayout.EndHorizontal();
			scrollPosition_sceneMeshes = EditorGUILayout.BeginScrollView(scrollPosition_sceneMeshes, false, true, hScrollbarStyle,vScrollbarStyle, scrollwindowStyle);
			var sceneReferenceManager= SceneReferenceManager.GetSceneReferenceManager(selected_scene);
			if (sceneReferenceManager)
			{
				foreach (var s in SceneReferenceManager.GetObjectMeshMap(selected_scene))
				{
					if(s.Key)
					if (resourceSearchText.Length > 0)
					{
						if (!SearchStringMatch(s.Key.name,resourceSearchText) && !SearchStringMatch(s.Value.name,resourceSearchText))
						{
							continue;
						}
					} 
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField(s.Key?s.Key.name:"null");
					EditorGUILayout.LabelField(s.Value?s.Value.name:"null");
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Resource", GUILayout.Width(150));
			EditorGUILayout.LabelField("Type", GUILayout.Width(150));
			EditorGUILayout.LabelField("Resource Path");
			EditorGUILayout.EndHorizontal();
			scrollPosition_sceneResourcePaths = EditorGUILayout.BeginScrollView(scrollPosition_sceneResourcePaths, false, true, hScrollbarStyle, vScrollbarStyle, scrollwindowStyle);
			var sceneResourcePathManager = SceneResourcePathManager.GetSceneResourcePathManager(selected_scene);
			if (sceneResourcePathManager)
			{
				foreach (var s in sceneResourcePathManager.GetResourcePathMap())
				{
					if(s.Key)
					if (resourceSearchText.Length > 0)
					{
						if (!(SearchStringMatch(s.Key.name,resourceSearchText)) && !SearchStringMatch(s.Key.GetType().ToString(),resourceSearchText) &&!SearchStringMatch(s.Value,resourceSearchText))
						{
							continue;
						}
					}
					EditorGUILayout.BeginHorizontal();
					if (s.Key != null)
					{
						EditorGUILayout.LabelField(s.Key?s.Key.name:"null", GUILayout.Width(150));
						EditorGUILayout.LabelField(s.Key?s.Key.GetType().ToString().Replace("UnityEngine.",""):"", GUILayout.Width(150));
						EditorGUILayout.LabelField(s.Value);
					}
					else
					{
						EditorGUILayout.LabelField("Null key", GUILayout.Width(150));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
		}
		string resourceSearchText="";
		void DrawResourcesLayout()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Name", GUILayout.Width(150));
            EditorGUILayout.LabelField("Type", GUILayout.Width(80));
            EditorGUILayout.LabelField("Uid", GUILayout.Width(80));
            EditorGUILayout.LabelField("Path");
            EditorGUILayout.EndHorizontal();
			foreach (var u in GeometrySource.GetGeometrySource().GetSessionResourceUids())
			{
				if (resourceSearchText.Length > 0)
				{
					if(u.Key!=null)
					if (!(SearchStringMatch(u.Key.name,resourceSearchText)) && !SearchStringMatch(u.Value.ToString(),resourceSearchText))
					{
						continue;
					}
				}
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(u.Key.name, GUILayout.Width(150));
                EditorGUILayout.LabelField(u.Key.GetType().ToString().Replace("UnityEngine.",""), GUILayout.Width(80));
                EditorGUILayout.LabelField(u.Value.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField(GeometrySource.GetPathFromUid(u.Value));
                EditorGUILayout.EndHorizontal();
			}
		}
		private void DrawSetupLayout()
		{
			GUI.enabled = !Application.isPlaying;
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
			GUI.enabled = true;
		}

		private void DrawDebugLayout()
		{
			EditorGUILayout.BeginVertical();
			GUI.enabled = !Application.isPlaying;

			if(GUILayout.Button("Clear Cached Data"))
			{
				geometrySource.ClearData();

				foreach(RenderTexture texture in renderTextures)
				{
					if(texture)
					{
						texture.Release();
					}
				}
				renderTextures = new RenderTexture[0];
			}
			GUI.enabled = true;

			//Create a new file explorer window, displaying the folder where compressed textures are saved.
			if (GUILayout.Button("Open Compressed Texture Folder"))
			{
				//File explorer will treat paths with forward slashes as invalid.
				System.Diagnostics.Process.Start("explorer.exe", geometrySource.compressedTexturesFolderPath.Replace('/', '\\'));
			}

			GUI.enabled = !Application.isPlaying;
			if (GUILayout.Button("Reload Geometry From Disk"))
			{
				geometrySource.LoadFromDisk();
			}
			if (GUILayout.Button($"Reset all masks."))
			{
				EditorMask.ResetAll();
			}
			GUI.enabled = true;
			EditorGUILayout.EndVertical();
		}


		private bool ExtractGeometry(List<GameObject> extractionList, GeometrySource.ForceExtractionMask forceMask)
		{
			for (int i = 0; i < extractionList.Count; i++)
			{
				GameObject gameObject = extractionList[i];
				if(EditorUtility.DisplayCancelableProgressBar($"Extracting Geometry ({i + 1} / {extractionList.Count})", $"Processing \"{gameObject.name}\".", (float)(i + 1) / extractionList.Count))
				{
					return false;
				}
				geometrySource.AddNode(gameObject, forceMask,false,verifyGeometry);
			}
			if (!geometrySource.ExtractTextures((forceMask & GeometrySource.ForceExtractionMask.FORCE_TEXTURES) == GeometrySource.ForceExtractionMask.FORCE_TEXTURES))
				return false;
			geometrySource.SaveToDisk();
			return true;
		}

		private void ExtractSelectedGeometry(GeometrySource.ForceExtractionMask forceMask)
		{
            List<GameObject> objectsToExtract = new List<GameObject>(Selection.gameObjects);
			ExtractGeometry(objectsToExtract, forceMask);
		}

		private bool ExtractSceneGeometry(GeometrySource.ForceExtractionMask forceMask)
		{
			var scene = SceneManager.GetActiveScene();
            List<GameObject> objectsToExtract = geometrySource.GetStreamableObjects(scene);
			ExtractGlobalIlluminationTextures(scene);
			return ExtractGeometry(objectsToExtract, forceMask);
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

				if (!ExtractSceneGeometry(forceMask))
					return;
			}

			//Re-open scenes that were originally open.
			EditorSceneManager.OpenScene(originalScenes[0]);
			for(int i = 1; i < originalScenes.Length; i++)
			{
				EditorSceneManager.OpenScene(originalScenes[i], OpenSceneMode.Additive);
			}
		}
		private void ExtractDynamicObjectLightingTextures()
		{
			if (Monitor.Instance)
			{
				if (Monitor.Instance.diffuseRenderTexture != null)
				{
					geometrySource.AddTexture(Monitor.Instance.diffuseRenderTexture, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
					geometrySource.ExtractTextures(true);
				}
				if (Monitor.Instance.specularRenderTexture != null)
				{
					geometrySource.AddTexture(Monitor.Instance.specularRenderTexture, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
					geometrySource.ExtractTextures(true);
				}
			}
		}
		private void ExtractGlobalIlluminationTextures(Scene scene)
		{
			Texture[] giTextures =teleport.GlobalIlluminationExtractor.GetTextures();
			if(giTextures==null)
				return;
			foreach (Texture texture in giTextures)
			{
				geometrySource.AddTexture(texture, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
			}
			geometrySource.ExtractTextures(true);
		}
		private void SetupGameObjectAndChildrenForStreaming(GameObject o)
		{
			foreach(var mf in o.GetComponentsInChildren<MeshFilter>())
			{
				var renderer = mf.gameObject.GetComponent<Renderer>();
				if (renderer == null || !renderer.enabled)
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
				if(go.GetComponentsInParent<MeshFilter>(true).Length ==
					go.GetComponents<MeshFilter>().Length)
				{
					go.tag = teleportSettings.TagToStream;
					BoxCollider bc = go.AddComponent<BoxCollider>();
					bc.isTrigger = true;
				}
			}
		}
		void OnHierarchyChange()
		{
			EditorMask.ResetAll();
		}

		void OnSelectionChange()
		{
			EditorMask.ResetAll();
		}

	}
}
