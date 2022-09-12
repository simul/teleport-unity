using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace teleport
{
	/// <summary>
	/// A class to store the connection between GameObjects and Meshes, which is lost when meshes are merged at the start of play.
	/// </summary>
	[Serializable]
	public class SceneReferenceManager : ScriptableObject, ISerializationCallbackReceiver
	{
		//Used to serialise dictionary, so it can be refilled when the object is deserialised.
		#region CustomSerialisation
		bool isAwake = false;
		[SerializeField, HideInInspector] string[] gameObjectReferences_keys;
		[SerializeField, HideInInspector] ResourceReferences[] gameObjectReferences_values;
		#endregion

		[Serializable]
		//References a GameObject has to resources it uses.
		public struct ResourceReferences
		{
			public Mesh mesh;
		}

		[Serializable]
		//Struct used to save references to disk in persistent format.
		private struct ReferencesSaveFormat
		{
			public string gameObjectPath;

			public string meshGUID;
			public string meshAssetPath;
			public string meshName;
		}

		private const string RESOURCES_PATH = "Assets/Resources/";
		private const string TELEPORT_VR_PATH = "TeleportVR/";
		//<GameObject Hierarchy Path, ResourceReferences of GameObject>.
		private Dictionary<string, ResourceReferences> gameObjectReferences = new Dictionary<string, ResourceReferences>(); 

		private static string directoryLocation; //Directory we save persistent references file to.
		private static string fileLocation; //File path of persistent references file.

		private static SceneReferenceManager instance;

		//STATIC FUNCTIONS

		public static SceneReferenceManager GetSceneReferenceManager()
		{
			if(instance == null)
			{
				instance = Resources.Load<SceneReferenceManager>(TELEPORT_VR_PATH + nameof(SceneReferenceManager));
			}

#if UNITY_EDITOR
			if(instance == null)
			{
				TeleportSettings.EnsureAssetPath(RESOURCES_PATH + TELEPORT_VR_PATH);
				string assetPath = RESOURCES_PATH + TELEPORT_VR_PATH + nameof(SceneReferenceManager) + ".asset";

				instance = CreateInstance<SceneReferenceManager>();
				UnityEditor.AssetDatabase.CreateAsset(instance, assetPath);
				UnityEditor.AssetDatabase.SaveAssets();

				Debug.LogWarning($"Scene Reference Manager asset created with path \"{assetPath}\"!");
			}
#endif

			return instance;
		}

		///PUBLIC FUNCTIONS

		//Add GameObject to SceneReferenceManager; extracting references to resources it uses.
		public ResourceReferences AddGameObject(GameObject gameObject, string gameObjectPath = null)
		{
			if(!gameObject)
			{
				Debug.LogError("Passed null GameObject to SceneReferenceManager!");
			}

			//Retrieve path of GameObject, if gameObjectPath is null.
			gameObjectPath = gameObjectPath ?? GetGameObjectPath(gameObject);
			//Only return what we have in the dictionary, if we are in play-mode; we want to re-extract if we are in the editor to update to any changes made.
			if(Application.isPlaying && gameObjectReferences.TryGetValue(gameObjectPath, out ResourceReferences references))
			{
				return references;
			}

			references.mesh = GetMesh(gameObject);
			//Don't edit the lookup table when we are playing, as the data may be incorrect; i.e. statically-combined meshes will give you null.
			if(Application.isPlaying)
			{
				return references;
			}

			gameObjectReferences[gameObjectPath] = references;
			return references;
		}

		//Get references to mesh used by GameObject. Will add the GameObject to the SceneReferenceManager, if it has not already been added.
		//Returns null if the GameObject does not use a mesh.
		public Mesh GetMeshFromGameObject(GameObject gameObject)
		{
			string gameObjectPath = GetGameObjectPath(gameObject);
			// We want to re-extract the references if we are not in play-mode; i.e. we want to update to changes made in the editor.
			if(!Application.isPlaying || !gameObjectReferences.TryGetValue(gameObjectPath, out ResourceReferences references))
			{
				references = AddGameObject(gameObject, gameObjectPath);
			}

			return references.mesh;
		}

		public void Clear()
		{
			gameObjectReferences.Clear();
			SaveToDisk();
		}

		//Saves ResourceReferences to disk.
		public void SaveToDisk()
		{
			if(!Directory.Exists(directoryLocation))
			{
				Directory.CreateDirectory(directoryLocation);
			}

			FileStream file;
			if(File.Exists(fileLocation))
			{
				file = File.OpenWrite(fileLocation);
			}
			else
			{
				file = File.Create(fileLocation);
			}

			BinaryFormatter binaryFormatter = new BinaryFormatter();
			binaryFormatter.Serialize(file, gameObjectReferences.Count);
			foreach(var referencePair in gameObjectReferences)
			{
				ReferencesSaveFormat saveFormat = ToSaveFormat(referencePair.Value, referencePair.Key);

				//We use JSON to more easily adapt to structure changes in the save format.
				string json = JsonUtility.ToJson(saveFormat);
				binaryFormatter.Serialize(file, json);
			}
			file.Close();
		}

		//Loads ResourceReferences from disk.
		public bool LoadFromDisk()
		{
			FileStream file;
			if(File.Exists(fileLocation))
			{
				file = File.OpenRead(fileLocation);
			}
			else
			{
				return false;
			}

			// Clear out old data.
			gameObjectReferences.Clear();

			BinaryFormatter binaryFormatter = new BinaryFormatter();
			int referenceCount = (int)binaryFormatter.Deserialize(file);
			for(int i = 0; i < referenceCount; i++)
			{
				//We use JSON to more easily adapt to structure changes in the save format.
				string json = (string)binaryFormatter.Deserialize(file);
				ReferencesSaveFormat saveFormat = JsonUtility.FromJson<ReferencesSaveFormat>(json);

				ResourceReferences resourceReferences = FromSaveFormat(saveFormat, out string gameObjectPath);
				gameObjectReferences[gameObjectPath] = resourceReferences;
			}
			file.Close();

			return true;
		}

		///INHERITED FUNCTIONS

		public void OnBeforeSerialize()
		{
			//Save everything to serialisable arrays, before the dictionary is discarded by Unity.
			gameObjectReferences_keys = gameObjectReferences.Keys.ToArray();
			gameObjectReferences_values = gameObjectReferences.Values.ToArray();
			gameObjectReferences.Clear();
		}

		public void OnAfterDeserialize()
		{
			//Don't run during boot.
			if(isAwake)
			{
				//Re-create the dictionary from the deserialised arrays.
				for(int i = 0; i < gameObjectReferences_keys.Length; i++)
				{
					gameObjectReferences[gameObjectReferences_keys[i]] = gameObjectReferences_values[i];
				}
			}
		}

		///REFLECTION FUNCTIONS

		private void Awake()
		{
			isAwake = true;
		}

		private void OnEnable()
		{
			//Must be done in OnEnable, as this is a ScriptableObject.
			directoryLocation = $"{Application.persistentDataPath}/TeleportVR/";
			fileLocation = $"{directoryLocation}/resource_references.dat";
			LoadFromDisk();
		}

		private void OnDisable()
		{
			SaveToDisk();
		}

		///PRIVATE FUNCTIONS

		//Returns path of GameObject in scene hierarchy.
		private string GetGameObjectPath(GameObject gameObject)
		{
			string gameObjectPath = gameObject.name;

			//Add parent Transforms to path.
			Transform currentTransform = gameObject.transform;
			while(currentTransform.parent != null)
			{
				currentTransform = currentTransform.parent;
				gameObjectPath = $"{currentTransform.name}/{gameObjectPath}";
			}

			//Add scene to path, if it exists; i.e. won't exist for prefab "scene".
			Scene scene = gameObject.scene;
			if(scene != null)
			{
				gameObjectPath = $"{scene.name}/{gameObjectPath}";
			}

			return gameObjectPath;
		}

#if UNITY_EDITOR
		public static bool GetResourcePath(UnityEngine.Object obj, out string path)
		{
			long localId = 0;
			string guid;
			bool result = UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId);
			if (!result)
			{ 
				path="";
				return false;
			}
			path = UnityEditor.AssetDatabase.GetAssetPath(obj);
			// Problem is, Unity can bundle a bunch of individual meshes in one asset file, but use them completely independently.
			// We can't therefore just use the file name.
			if(obj.GetType()==typeof(UnityEngine.Mesh))
			{
				path=Path.GetDirectoryName(path);
				path=Path.Combine(path,obj.name);
			}
			path=path.Replace("Assets/","");
			// Need something unique. Within default and editor resources are thousands of assets, often with clashing names.
			// So here, we do use the localId's to distinguish them.
			if (path.Contains("unity default resources"))
			{
				path +="/"+obj.name+"_"+ localId;
			}
			if (path.Contains("unity editor resources"))
			{
				path += "/" + obj.name + "_" + localId;
			}
			return true;
		}
		public static bool GetGUIDAndLocalFileIdentifier(UnityEngine.Object obj, out string guid)
		{
			long localId =0;
			bool result= UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId);
			if(!result)
				return false;
			string localIdString= String.Format("{0:X8}", localId);
			guid=guid+localIdString;
			return true;
		}
#endif
		//Returns first-found mesh or skinned mesh on the GameObject; on the object itself, or its child-hierarchy.
		private Mesh GetMesh(GameObject gameObject)
		{
			MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
			if(meshFilter)
			{
				if(meshFilter.sharedMesh!=null&&meshFilter.sharedMesh.isReadable)
				{
					return meshFilter.sharedMesh;
				}
				else
				{
#if UNITY_EDITOR
					string assetPath= UnityEditor.AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
					UnityEditor.ModelImporter modelImporter= UnityEditor.ModelImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
					if (modelImporter != null)
					{
						modelImporter.isReadable=true;
						modelImporter.SaveAndReimport();
					}
#endif
					if (!meshFilter.sharedMesh)
					{
						Debug.LogWarning($"sharedMesh is null on {gameObject.name}!");
						return null;
					}
					if (!meshFilter.sharedMesh.isReadable)
					{
						Debug.LogWarning($"Can't extract {meshFilter.sharedMesh} on {gameObject.name}, as the mesh is not readable!");
					}
					return meshFilter.sharedMesh;
				}
			}

			SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
			if(skinnedMeshRenderer)
			{
				if(skinnedMeshRenderer.sharedMesh.isReadable)
				{
					return skinnedMeshRenderer.sharedMesh;
				}
				else
				{
#if UNITY_EDITOR
					string assetPath = UnityEditor.AssetDatabase.GetAssetPath(skinnedMeshRenderer.sharedMesh);
					UnityEditor.ModelImporter modelImporter = UnityEditor.ModelImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
					if (modelImporter != null)
					{
						modelImporter.isReadable = true;
						modelImporter.SaveAndReimport();
					}
#endif
					if (skinnedMeshRenderer.sharedMesh.isReadable)
					{
						return skinnedMeshRenderer.sharedMesh;
					}
					Debug.LogWarning($"Can't extract {skinnedMeshRenderer.sharedMesh} on {gameObject.name}, as the mesh is not readable!");
				}
			}

			return null;
		}

		//FILE READ/WRITE FUNCTIONS

		//Converts ResourceReferences to ReferencesSaveFormat.
		private ReferencesSaveFormat ToSaveFormat(ResourceReferences resourceReferences, string gameObjectPath)
		{
#if UNITY_EDITOR
			ReferencesSaveFormat saveFormat = new ReferencesSaveFormat();
			saveFormat.gameObjectPath = gameObjectPath;

			if(resourceReferences.mesh)
			{
				GetGUIDAndLocalFileIdentifier(resourceReferences.mesh, out saveFormat.meshGUID);
				saveFormat.meshAssetPath = UnityEditor.AssetDatabase.GetAssetPath(resourceReferences.mesh);
				saveFormat.meshName = resourceReferences.mesh.name;
			}

			return saveFormat;
#else
		return new ReferencesSaveFormat();
#endif
		}

		//Converts ReferencesSaveFormat to ResourceReferences; outputs path of game object as second parameter.
		private ResourceReferences FromSaveFormat(ReferencesSaveFormat saveFormat, out string gameObjectPath)
		{
#if UNITY_EDITOR
			ResourceReferences resourceReferences = new ResourceReferences();
			//Load all assets at path; there may be multiple meshes sharing a GUID, but differentiated by name.
			if (saveFormat.meshGUID.Length < 32)
			{
				Debug.LogWarning($"meshGUID "+saveFormat.meshGUID+" is too small!");
				gameObjectPath="";
				return resourceReferences;
			}
			string meshAssetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(saveFormat.meshGUID.Substring(0,32));
			UnityEngine.Object[] assetsAtPath = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(meshAssetPath);

			//Match meshes at the path with the same mesh name; this should only give one result.
			List<UnityEngine.Object> meshesAtPath = new List<UnityEngine.Object>(assetsAtPath.Where(i => i.name == saveFormat.meshName && (i.GetType() == typeof(Mesh) || i.GetType().IsSubclassOf(typeof(Mesh)))));
			if(meshesAtPath.Count != 0)
			{
				resourceReferences.mesh = (Mesh)meshesAtPath[0];
			}

			gameObjectPath = saveFormat.gameObjectPath;
			return resourceReferences;
#else
		gameObjectPath = "";
		return new ResourceReferences();
#endif
		}
	}
}
