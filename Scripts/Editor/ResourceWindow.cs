using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;
using uid = System.UInt64;

namespace teleport
{
	public class ResourceWindow : EditorWindow
	{
		public static bool SearchStringMatch(string source, string toCheck)
		{
			return source?.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		enum ResourceWindowCategories { TAG_SETUP, COLLISION_SETUP,RESOURCES, SCENE_REFS, DEBUG};

		//References to assets.
		private GeometrySource geometrySource;
		private ComputeShader textureShader;
		private TeleportSettings teleportSettings;

		//List of data that was extracted in the last extraction operation.
		private RenderTexture[] renderTextures = new RenderTexture[0];
		private bool verifyGeometry = false;
		private bool forceExtraction=false;
		private bool fixBadMeshes=false;
		//Text styles.
		private GUIStyle richText ;
		private GUIStyle warningText ;
		private GUIStyle errorText ;
		private GUIStyle hScrollbarStyle;
		private GUIStyle vScrollbarStyle;
		private GUIStyle scrollwindowStyle;
		private GUIStyle labelTextStyle ;
		private GUIStyle titleStyle;
		private GUIStyle groupStyle;

		//GUI variables that control user-changeable properties.
		private Vector2 scrollPosition_gameObjects;
		private Vector2 scrollPosition_textures;
	//	private bool foldout_gameObjects = false;
		private bool foldout_textures = false;
		private Vector2 scrollPosition_meshTable;

		private string[] categories;
		private int selectedCategory = 0;

		[MenuItem("Teleport VR/Resource Manager",false,1000)]
		public static void OpenResourceWindow()
		{
			ResourceWindow window = GetWindow<ResourceWindow>(false, "Teleport Resources");
			window.minSize = new Vector2(600, 200);
			window.Show();
		}

		private void Awake()
		{
			labelTextStyle = null;
			geometrySource = GeometrySource.GetGeometrySource();

			string shaderGUID = AssetDatabase.FindAssets("ExtractTextureData t:ComputeShader")[0];
			textureShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID));

			teleportSettings = TeleportSettings.GetOrCreateSettings();
		}

		//Use this to setup variables that are likely to change on a hot-reload.
		private void OnFocus()
		{
			labelTextStyle=null;
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
			if (labelTextStyle==null)
			{
				richText = new GUIStyle(GUI.skin.textArea);
				warningText = new GUIStyle(GUI.skin.label);
				errorText = new GUIStyle(GUI.skin.label);
				hScrollbarStyle = new GUIStyle(GUI.skin.horizontalScrollbar);
				vScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
				labelTextStyle = new GUIStyle(GUI.skin.label);
				scrollwindowStyle = new GUIStyle(GUI.skin.box);
				titleStyle = new GUIStyle(EditorStyles.foldout);
				titleStyle.fontSize = (GUI.skin.label.fontSize * 5) / 4;
				titleStyle.fontStyle=FontStyle.Bold;
				titleStyle.padding=new RectOffset(20,10,10,10);
				titleStyle.border = new RectOffset(20, 10, 10, 10);

				groupStyle =new GUIStyle(GUI.skin.box);

				richText.normal.textColor = Color.white;


				richText.richText = true;

				warningText.normal.textColor = new Color(1.0f, 1.0f, 0.0f);
				errorText.normal.textColor = Color.red;
			}
			DrawExtractionLayout();
			EditorGUILayout.Separator();
			foldoutAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutAdvanced, "Advanced", titleStyle);
			if (foldoutAdvanced)
			{ 
				EditorGUILayout.BeginHorizontal();
				selectedCategory = GUILayout.SelectionGrid(selectedCategory, categories, 1, GUILayout.ExpandWidth(false));

				EditorGUILayout.BeginVertical();
				resourceSearchText = EditorGUILayout.TextField("Search ", resourceSearchText);
				switch ((ResourceWindowCategories)selectedCategory)
				{
					case ResourceWindowCategories.TAG_SETUP:
						DrawTagSetupLayout();
						break; 
					case ResourceWindowCategories.COLLISION_SETUP:
						DrawCollisionSetupLayout();
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
			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		private void DrawExtractionLayout()
		{
			foldoutRoots = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutRoots, "Selection", titleStyle);

			if (foldoutRoots)
			{
				EditorGUILayout.BeginVertical(groupStyle);
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Choose objects that should be streamed as geometry.");
				if (EditorGUILayout.LinkButton("Help"))
				{
					System.Diagnostics.Process.Start("explorer", "https://docs.teleportvr.io/unity/index.html#resource-management");
				}
				EditorGUILayout.EndHorizontal();
				if (GUILayout.Button("Select All Roots"))
				{
					Selection.objects = GetStreamableRoots(-1000000, 1000000).ToArray();
				}
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Select by Priority"))
				{
					Selection.objects = GetStreamableRoots(minPrior, maxPrior).ToArray();
				}
				EditorGUILayout.LabelField("Min:", GUILayout.Width(60));
				int nextMinPrior = EditorGUILayout.IntField(minPrior, GUILayout.Width(60));
				if (nextMinPrior != minPrior)
				{
					minPrior = nextMinPrior;
					if (maxPrior < minPrior)
						maxPrior = minPrior;
				}
				EditorGUILayout.LabelField("Max:", GUILayout.Width(60));
				int nextMaxPrior = EditorGUILayout.IntField(maxPrior, GUILayout.Width(60));
				if (nextMaxPrior != maxPrior)
				{
					maxPrior = nextMaxPrior;
					if (maxPrior < minPrior)
						minPrior = maxPrior;
				}

				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				EditorGUILayout.Space(10);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			foldoutExtraction= EditorGUILayout.BeginFoldoutHeaderGroup(foldoutExtraction, "Extraction", titleStyle);
			if(foldoutExtraction)
			{ 
				EditorGUILayout.Space(10);
				GUI.enabled = !Application.isPlaying;
				labelTextStyle.alignment=TextAnchor.MiddleRight;
				EditorGUILayout.Space(10);
				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.BeginVertical();
				{
					UnityEngine.Object[] activeGOs =
						Selection.GetFiltered(
							typeof(GameObject),
							 SelectionMode.TopLevel);
					{ 
						bool wasEnabled=GUI.enabled;
						GUI.enabled = activeGOs.Length>0;
					
						EditorGUILayout.BeginHorizontal(); 
						GUILayout.Label("Selected Geometry:", labelTextStyle, GUILayout.Width(300));
						if (GUILayout.Button("Extract"))
						{
							ExtractSelectedGeometry(forceExtraction? GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES:GeometrySource.ForceExtractionMask.FORCE_NODES);
						}
						EditorGUILayout.EndHorizontal();
						GUI.enabled=wasEnabled;
					}
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label("Scene Geometry:", labelTextStyle, GUILayout.Width(300));
					if (GUILayout.Button("Extract"))
					{
						ExtractSceneGeometry(forceExtraction ? GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES : GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label("Project Geometry:", labelTextStyle, GUILayout.Width(300));
					if (GUILayout.Button("Extract"))
					{
						ExtractProjectGeometry(forceExtraction ? GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES : GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label("Global Illumination Textures:", labelTextStyle, GUILayout.Width(300));
					if (GUILayout.Button("Extract"))
					{
						ExtractGlobalIlluminationTextures();
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label("Dynamic Object Lighting Textures:", labelTextStyle, GUILayout.Width(300));
					bool wasEnabled2 = GUI.enabled;
					GUI.enabled &= (Monitor.Instance?Monitor.Instance.envMapsGenerated:false);

					if (GUILayout.Button("Extract"))
					{
						for (int i = 0; i < SceneManager.sceneCount; i++)
						{
							var scene = SceneManager.GetSceneAt(i);
							if (scene == null)
								continue;
							ExtractDynamicObjectLightingTextures(scene);
						}
					}
					GUI.enabled = wasEnabled2;
					GUILayout.Label("Background Texture:", labelTextStyle, GUILayout.Width(300));

					if (GUILayout.Button("Extract"))
					{
						for (int i = 0; i < SceneManager.sceneCount; i++)
						{
							var scene = SceneManager.GetSceneAt(i);
							if (scene == null)
								continue;
							ExtractBackgroundTexture(scene);
						}
					}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUILayout.EndVertical();


				EditorGUILayout.BeginVertical();
					verifyGeometry = GUILayout.Toggle(verifyGeometry, "Verify Compressed Geometry");
					forceExtraction = GUILayout.Toggle(forceExtraction, "Force Extraction");
					geometrySource.treatTransparentAsDoubleSided = GUILayout.Toggle(geometrySource.treatTransparentAsDoubleSided, "Treat transparent as double-sided");
				EditorGUILayout.EndVertical();

				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
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
				if(u.Key==null)
					continue;
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
		private void DrawTagSetupLayout()
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
			if (teleportSettings.TagToStream.Length > 0)
			{
				GameObject[] gameObjectsTagged = GameObject.FindGameObjectsWithTag(teleportSettings.TagToStream);

				EditorGUILayout.LabelField($"There are <b>{gameObjectsTagged.Length}</b> GameObjects with the '{teleportSettings.TagToStream}' tag.", richText);

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label($"Remove Streamable Node and Streamable Root from all GameObjects.", labelTextStyle, GUILayout.Width(500));
				if (GUILayout.Button("Apply", GUILayout.Width(100)))
				{
					StreamableNode [] n =GameObject.FindObjectsByType<StreamableNode>(FindObjectsSortMode.None);
					foreach (var sn in n)
					{
						DestroyImmediate(sn);
					}
					StreamableRoot[] r = GameObject.FindObjectsByType<StreamableRoot>(FindObjectsSortMode.None);
					foreach (var sr in r)
					{
						DestroyImmediate(sr);
					}
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label($"Clear tag '{teleportSettings.TagToStream}' from all GameObjects.", labelTextStyle, GUILayout.Width(500));
				if (GUILayout.Button("Apply", GUILayout.Width(100)))
				{
					foreach(var gameObject in gameObjectsTagged)
					{
						gameObject.tag = "Untagged";
					}
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"Apply the tag {teleportSettings.TagToStream} to selected objects with collision.", labelTextStyle, GUILayout.Width(500));
				if (GUILayout.Button($"Apply", GUILayout.Width(100)))
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
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"Apply the tag to objects within selected collider.", labelTextStyle, GUILayout.Width(500));
				List<Collider> colliders= new List<Collider>();
				foreach (var gameObject in UnityEditor.Selection.gameObjects)
				{
					colliders.Add(gameObject.GetComponent<BoxCollider>());
				}
				bool was_enabled=GUI.enabled ;
				GUI.enabled &= colliders.Count>0&& teleportSettings.TagToStream!=null&&teleportSettings.TagToStream.Length>0;
				if (GUILayout.Button($"Apply", GUILayout.Width(100)))
				{
					foreach (var c in colliders)
					{
						BoxCollider bc=c.GetComponent<BoxCollider>();
						if (bc)
						{
							for (int i = 0; i < SceneManager.sceneCount; i++)
							{
								var scene = SceneManager.GetSceneAt(i);
								if (scene == null)
									continue;
								var objs = scene.GetRootGameObjects();
								foreach (var o in objs)
								{
									UnityEngine.Transform[] transforms = o.GetComponentsInChildren<UnityEngine.Transform>();
									foreach (var t in transforms)
									{
										if (bc.bounds.Contains(t.position))
										{
											t.gameObject.tag= teleportSettings.TagToStream;
										}
									}
								}
							}
						}
					}
				}
				GUI.enabled = was_enabled;
				EditorGUILayout.EndHorizontal();
			}
			GUI.enabled = true;
		}
		private void DrawCollisionSetupLayout()
		{
			GUI.enabled = !Application.isPlaying;

			EditorGUILayout.LabelField("Adds a trigger box collider to selected GameObjects with a MeshFilter, but without a collider.");
			if (GUILayout.Button("Add box collider to selected GameObjects."))
			{
				foreach (var gameObject in Selection.gameObjects)
				{
					var collider = gameObject.GetComponent<Collider>();
					var meshFilter = gameObject.GetComponent<MeshFilter>();
					if (meshFilter != null && collider == null)
					{
						BoxCollider bc = gameObject.AddComponent<BoxCollider>();
						bc.isTrigger = true;
					}
				}
			}

			EditorGUILayout.LabelField("Recursively adds box collision and tag to selected and children where appropriate.");
			if (GUILayout.Button("Setup selected and children for streaming."))
			{
				foreach (var gameObject in Selection.gameObjects)
				{
					SetupGameObjectAndChildrenForStreaming(gameObject);
				}
			}
			GUI.enabled = true;
		}
		int minPrior=0, maxPrior=0;
		bool foldoutRoots=true;
		bool foldoutExtraction=true;
		bool foldoutAdvanced=false;
		List<GameObject> GetStreamableRoots(int minPrior,int maxPrior)
		{
			List<GameObject> rootGameObjects = new List<GameObject>();
			for (int i = 0; i<SceneManager.sceneCount; i++)
			{
				var objs = SceneManager.GetSceneAt(i).GetRootGameObjects();
				foreach (var o in objs)
				{
					teleport.StreamableRoot[] m = o.GetComponentsInChildren<teleport.StreamableRoot>();
					foreach(var r in m)
					{
						if(r.priority>= minPrior&&r.priority<=maxPrior)
							rootGameObjects.Add(r.gameObject);
					}
				}
			}
			return rootGameObjects;
		}
		private void DrawDebugLayout()
		{
			EditorGUILayout.BeginVertical();
			GUI.enabled = !Application.isPlaying;
			if (GUILayout.Button("Test BC6H Texture"))
			{
				geometrySource.AddFp16TestTexture();
			}
			if (GUILayout.Button("Clear Cached Data"))
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
			if (GUILayout.Button($"Find bad meshes."))
			{
				EditorUtility.ClearProgressBar();
				bool end=false;
				var sel = Selection.gameObjects;
				for (int i = 0; i < SceneManager.sceneCount; i++)
				{
					var scene = SceneManager.GetSceneAt(i);
					if (scene == null)
						continue;
					if (end)
						break;
					var objs = scene.GetRootGameObjects();
					if(sel.Length>0)
						objs=sel;
					for (int j=0;j<objs.Length; j++)
					{
						if(end)
							break;
						if (EditorUtility.DisplayCancelableProgressBar($"Checking for bad Geometry", $"Scene {i}, object {j}", (float)(j + 1) / (float)(objs.Length)))
						{
							EditorUtility.ClearProgressBar();
							end =true;
						}
						geometrySource.FindBadMeshes(objs[j], fixBadMeshes);
					}
					if(sel.Length > 0)
						break;
				}
				EditorUtility.ClearProgressBar();
			}
			fixBadMeshes = GUILayout.Toggle(fixBadMeshes, "Fix Bad Meshes");
			GUI.enabled = true;
			EditorGUILayout.EndVertical();
		}


		private bool ExtractGameObject(GameObject gameObject, GeometrySource.ForceExtractionMask forceMask)
		{
			geometrySource.AddNode(gameObject, forceMask, false, verifyGeometry);
			return true;
		}
		private bool ExtractGeometry(List<GameObject> extractionList, GeometrySource.ForceExtractionMask forceMask)
		{
			for (int i = 0; i < extractionList.Count; i++)
			{
				GameObject gameObject = extractionList[i];
				if (EditorUtility.DisplayCancelableProgressBar($"Extracting Geometry ({i + 1} / {extractionList.Count})", $"Processing \"{gameObject.name}\".", (float)(i + 1) / extractionList.Count))
				{
					EditorUtility.ClearProgressBar();
					return false;
				}
				geometrySource.AddNode(gameObject, forceMask, false, verifyGeometry);
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
			List<GameObject> objectsToExtract=new List<GameObject>();
			// Find all the prefabs we expect to use in this scene.
			if (Monitor.Instance != null)
			{
				if(Monitor.Instance.defaultPlayerPrefab != null)
					objectsToExtract.Add(Monitor.Instance.defaultPlayerPrefab);
				else
					objectsToExtract.Add(Resources.Load("Prefabs/DefaultUser") as GameObject);
			}
			for(int i=0;i<SceneManager.sceneCount;i++)
			{
				var scene = SceneManager.GetSceneAt(i);
				if(scene==null)
					continue;
				ExtractGlobalIlluminationTextures();
				objectsToExtract.AddRange(geometrySource.GetStreamableRoots(scene));
				if(TagHandler.Instance)
				{
					objectsToExtract.AddRange(TagHandler.Instance.GetTaggedObjects(scene));
				}
				ExtractDynamicObjectLightingTextures(scene);
				ExtractBackgroundTexture(scene);
			}
			if(!ExtractGeometry(objectsToExtract, forceMask))
				return false;
			return true;
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
		private void ExtractBackgroundTexture(Scene scene)
		{
			if (Monitor.Instance )
			{
				if (Monitor.Instance.environmentCubemap != null)
				{
					geometrySource.AddTexture(Monitor.Instance.environmentRenderTexture, Monitor.Instance.gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
					geometrySource.ExtractTextures(true);
				}
			}
		}
		private void ExtractDynamicObjectLightingTextures(Scene scene)
		{
			if (Monitor.Instance&& Monitor.Instance.envMapsGenerated )
			{
				if (Monitor.Instance.diffuseRenderTexture != null)
				{
					geometrySource.AddTexture(Monitor.Instance.diffuseRenderTexture, Monitor.Instance.gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
					geometrySource.ExtractTextures(true);
				}
				if (Monitor.Instance.specularRenderTexture != null)
				{
					geometrySource.AddTexture(Monitor.Instance.specularRenderTexture, Monitor.Instance.gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
					geometrySource.ExtractTextures(true);
				}
			}
		}
		private void ExtractGlobalIlluminationTextures()
		{
			Texture[] giTextures =teleport.GlobalIlluminationExtractor.GetTextures();
			if(giTextures==null)
				return;
			foreach (Texture texture in giTextures)
			{
				geometrySource.AddTexture(texture, Monitor.Instance.gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
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
