using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		private List<GameObject> lastExtractedGameObjects = new List<GameObject>(); //List of streamable GameObjects that were found during the last operation.
		private RenderTexture[] renderTextures = new RenderTexture[0];
		private bool includePlayerParts = true;
		private bool compressGeometry = true;

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
			includePlayerParts = GUILayout.Toggle(includePlayerParts, "Include Player Parts");
			compressGeometry = GUILayout.Toggle(compressGeometry, "Compress Geometry"); 

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
				ExtractSelectedGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Extract Scene Geometry"))
			{
				ExtractSceneGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
			}

			if(GUILayout.Button("Force Scene Geometry Extraction"))
			{
				ExtractSceneGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			if(GUILayout.Button("Extract Project Geometry"))
			{
				ExtractProjectGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
			}

			if(GUILayout.Button("Force Project Geometry Extraction"))
			{
				ExtractProjectGeometry(GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			if (GUILayout.Button("Extract Global Illumination Textures"))
			{
				ExtractGlobalIlluminationTextures();
			}

			EditorGUILayout.EndHorizontal();

			foldout_gameObjects = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_gameObjects, "Last Extracted GameObjects (" + lastExtractedGameObjects.Count + ")");
			if(foldout_gameObjects)
			{
				scrollPosition_gameObjects = EditorGUILayout.BeginScrollView(scrollPosition_gameObjects);

				foreach(GameObject gameObject in lastExtractedGameObjects)
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
				lastExtractedGameObjects = new List<GameObject>();

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
			if (GUILayout.Button($"Reset all masks."))
			{
				EditorMask.ResetAll();
			}
		}

		private void FindSceneStreamables()
		{
			lastExtractedGameObjects = geometrySource.GetStreamableObjects(includePlayerParts);
		}

		private void ExtractGeometry(List<GameObject> extractionList, GeometrySource.ForceExtractionMask forceMask)
		{
			if (!compressGeometry)
				forceMask = forceMask | GeometrySource.ForceExtractionMask.FORCE_UNCOMPRESSED;
			for (int i = 0; i < extractionList.Count; i++)
			{
				GameObject gameObject = extractionList[i];

				if(EditorUtility.DisplayCancelableProgressBar($"Extracting Geometry ({i + 1} / {extractionList.Count})", $"Processing \"{gameObject.name}\".", (float)(i + 1) / extractionList.Count))
				{
					return;
				}

				geometrySource.AddNode(gameObject, forceMask,false);
			}
			ExtractTextures();

			geometrySource.SaveToDisk();
		}

		private void ExtractSelectedGeometry(GeometrySource.ForceExtractionMask forceMask)
		{
			lastExtractedGameObjects = new List<GameObject>(Selection.gameObjects);
			ExtractGeometry(lastExtractedGameObjects, forceMask);
		}

		private void ExtractSceneGeometry(GeometrySource.ForceExtractionMask forceMask)
		{
			lastExtractedGameObjects = geometrySource.GetStreamableObjects(includePlayerParts);
			ExtractGeometry(lastExtractedGameObjects, forceMask);
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
		private void ExtractGlobalIlluminationTextures()
		{
			Texture giTexture=teleport.GlobalIlluminationExtractor.GetTexture();
			geometrySource.AddTexture(giTexture,GeometrySource.ForceExtractionMask.FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES);
			ExtractTextures();
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
			for (int i = 0; i < renderTextures.Length; i++)
			{
				bool highQualityUASTC = false;
				Texture2D sourceTexture = (Texture2D)geometrySource.texturesWaitingForExtraction[i].unityTexture;

				if (EditorUtility.DisplayCancelableProgressBar($"Extracting Textures ({i + 1} / {renderTextures.Length})", $"Processing \"{sourceTexture.name}\".", (float)(i + 1) / renderTextures.Length))
				{
					return;
				}
				int targetWidth = sourceTexture.width;
				int targetHeight = sourceTexture.height;
				int scale = 1;
				while (Math.Max(targetWidth, targetHeight) > teleportSettings.casterSettings.maximumTextureSize)
				{
					targetWidth = (targetWidth + 1) / 2;
					targetHeight = (targetHeight + 1) / 2;
					scale *= 2;
				}
				//If we always created a new render texture, then reloading would lose the link to the render texture in the inspector.
				if (renderTextures[i] == null)
				{
					renderTextures[i] = new RenderTexture(targetWidth, targetHeight, 0);
				}
				else
				{
					renderTextures[i].Release();
					renderTextures[i].width = targetWidth;
					renderTextures[i].height = targetHeight;
					renderTextures[i].depth = 0;
				}
				bool isNormal = false;
				//Normal maps need to be extracted differently; i.e. convert from DXT5nm format.
				string path = AssetDatabase.GetAssetPath(sourceTexture);
				TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
				if (textureImporter != null)
				{
					textureImporter.isReadable = true;
					TextureImporterType textureType = textureImporter ? textureImporter.textureType : TextureImporterType.Default;
					isNormal = textureType == UnityEditor.TextureImporterType.NormalMap;
				}
				switch (sourceTexture.format)
				{
					case TextureFormat.RGBAFloat:
						renderTextures[i].format = RenderTextureFormat.ARGB32;
						highQualityUASTC = false;
						break;
					case TextureFormat.BC6H:
						renderTextures[i].format = RenderTextureFormat.ARGB32;
						highQualityUASTC= true;
						break;
					case TextureFormat.RGBAHalf:
						renderTextures[i].format = RenderTextureFormat.ARGBHalf;
						break;
					case TextureFormat.RGBA32:
						renderTextures[i].format = RenderTextureFormat.ARGBInt;
						break;
					case TextureFormat.ARGB32:
						renderTextures[i].format = RenderTextureFormat.ARGB32;
						break;
					default:
						break;
				}
				if(isNormal)
					highQualityUASTC= false;
				renderTextures[i].enableRandomWrite = true;
				renderTextures[i].name = $"{geometrySource.texturesWaitingForExtraction[i].unityTexture.name} ({geometrySource.texturesWaitingForExtraction[i].id})";
				renderTextures[i].Create();

				string shaderName = isNormal ? "ExtractNormalMap" : "ExtractTexture";
				shaderName += UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma ? "Gamma" : "Linear";
				
				int kernelHandle = textureShader.FindKernel(shaderName);
				textureShader.SetTexture(kernelHandle, "Source", sourceTexture);
				textureShader.SetTexture(kernelHandle, "Result", renderTextures[i]);
				textureShader.SetInt("Scale",scale);
				textureShader.Dispatch(kernelHandle, targetWidth / 8, targetHeight / 8, 1);

				if (!SystemInfo.IsFormatSupported(renderTextures[i].graphicsFormat, UnityEngine.Experimental.Rendering.FormatUsage.Sample)) 
				{
					UnityEngine.Debug.LogError("Format of texture " + i + " is not supported for Texture2D.");
					continue;
				}

				//Rip data from render texture, and store in GeometryStore.
				{
					//Read pixel data into Texture2D, so that it can be read.
					Texture2D readTexture = new Texture2D(renderTextures[i].width, renderTextures[i].height, renderTextures[i].graphicsFormat
						, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

					{
						RenderTexture oldActive = RenderTexture.active;

						RenderTexture.active = renderTextures[i];
						readTexture.ReadPixels(new Rect(0, 0, renderTextures[i].width, renderTextures[i].height), 0, 0);
						readTexture.Apply();

						RenderTexture.active = oldActive;
					}
					avs.Texture textureData = geometrySource.texturesWaitingForExtraction[i].textureData;
					textureData.width=(uint)targetWidth;
					textureData.height=(uint)targetHeight;
					if (readTexture.format== TextureFormat.RGBAFloat)
					{
						Color[] pixelData = readTexture.GetPixels();
						textureData.format = avs.TextureFormat.RGBAFloat;
						textureData.bytesPerPixel = 16;

						int floatSize = Marshal.SizeOf<float>();
						textureData.dataSize = (uint)(pixelData.Length * 4 * floatSize);
						textureData.data = Marshal.AllocCoTaskMem((int)textureData.dataSize);

						int byteOffset = 0;
						foreach (Color pixel in pixelData)
						{
							float[] f= { pixel.r, pixel .g, pixel .b, pixel .a };
							Marshal.Copy(f, 0, textureData.data + byteOffset, 4);
							byteOffset += 4*floatSize;
						}
					}
					else
					{ 
						Color32[] pixelData = readTexture.GetPixels32();

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

					}
					// Test: write to png.
					if(highQualityUASTC)
					{
						string textureAssetPath = AssetDatabase.GetAssetPath(sourceTexture);
						byte[] bytes = readTexture.EncodeToPNG();
						string basisFile=geometrySource.GenerateCompressedFilePath(textureAssetPath,avs.TextureCompression.BASIS_COMPRESSED);
						string pngFile = basisFile.Replace(".basis", ".png");
						string dirPath = System.IO.Path.GetDirectoryName(pngFile);
						if (!Directory.Exists(dirPath))
						{
							Directory.CreateDirectory(dirPath);
						}
						File.WriteAllBytes(pngFile, bytes);
						// We will send the .png instead of a .basis file.
						//LaunchBasisUExe(pngFile);
						textureData.compression=avs.TextureCompression.PNG;
						textureData.dataSize = (uint)(bytes.Length );
						textureData.data = Marshal.AllocCoTaskMem((int)textureData.dataSize);
						Marshal.Copy(bytes,0,textureData.data,(int)textureData.dataSize);
						geometrySource.AddTextureData(sourceTexture, textureData, highQualityUASTC);
					}
					else
					{
						textureData.compression = avs.TextureCompression.BASIS_COMPRESSED;
						geometrySource.AddTextureData(sourceTexture, textureData, highQualityUASTC);
					}
					Marshal.FreeCoTaskMem(textureData.data);
				}
			}

			geometrySource.CompressTextures();
			geometrySource.texturesWaitingForExtraction.Clear();
		}
		/// <summary>
		/// Launch the application with some options set.
		/// </summary>
		static void LaunchBasisUExe(string srcPng)
		{
			string basis = "";
			string rootPath = Application.dataPath;
			// Because Basis is broken for UASTC when run internally, we instead call it directly here.
			string[] files = Directory.GetFiles(rootPath, "basisu.exe", SearchOption.AllDirectories);
			if (files.Length > 0)
			{
				basis = files[0];
			}
			else
            {
				UnityEngine.Debug.LogError("Failed to find basisu.exe for UASTC texture.");
				return;
            }

			// Use ProcessStartInfo class
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.CreateNoWindow = true;
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.FileName = basis;
			//startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			string outputPath = System.IO.Path.GetDirectoryName(srcPng);
			startInfo.Arguments = "-uastc -output_path \""+outputPath+"\" \"" + srcPng+"\"";

			try
			{
				// Start the process with the info we specified.
				// Call WaitForExit and then the using statement will close.
				using (Process exeProcess = Process.Start(startInfo))
				{
					exeProcess.WaitForExit();
					int exitCode=exeProcess.ExitCode;
					if (exitCode != 0)
                    {
						UnityEngine.Debug.LogError("Basis exit code " + exitCode);
                    }
				}
			}
			catch
			{
				// Log error.
			}
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

	}
}
