using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;

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

	static string directoryLocation; //Directory we save persistent references file to.
	static string fileLocation; //File path of persistent references file.

	Dictionary<string, ResourceReferences> gameObjectReferences = new Dictionary<string, ResourceReferences>(); //<GameObject Hierarchy Path, ResourceReferences of GameObject>.

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

		//Remove first, so we can overwrite current value with updated value.
		gameObjectReferences.Remove(gameObjectPath);
		gameObjectReferences.Add(gameObjectPath, references);
		return references;
	}

	//Get references to mesh used by GameObject. Will add the GameObject to the SceneReferenceManager, if it has not already been added.
	//Returns null if the GameObject does not use a mesh.
	public Mesh GetGameObjectMesh(GameObject gameObject)
	{
		string gameObjectPath = GetGameObjectPath(gameObject);

		//We want to re-extract the references if we are not in play-mode; i.e. we want to update to changes made in the editor.
		if(!Application.isPlaying || !gameObjectReferences.TryGetValue(gameObjectPath, out ResourceReferences references))
		{
			references = AddGameObject(gameObject, gameObjectPath);
		}

		return references.mesh;
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

	//Returns first-found mesh or skinned mesh on the GameObject; on the object itself, or its child-hierarchy.
	private Mesh GetMesh(GameObject gameObject)
	{
		MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
		if(meshFilter && meshFilter.sharedMesh.isReadable)
		{
			return meshFilter.sharedMesh;
		}

		SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
		if(skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh.isReadable)
		{
			return skinnedMeshRenderer.sharedMesh;
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
			UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(resourceReferences.mesh, out saveFormat.meshGUID, out long _);
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

		string meshAssetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(saveFormat.meshGUID);
		resourceReferences.mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

		if(!resourceReferences.mesh)
		{
			UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(saveFormat.meshAssetPath);
			List<UnityEngine.Object> matched = new List<UnityEngine.Object>(assets.Where(i => i.name == saveFormat.meshName && i.GetType() == typeof(Mesh)));

			//Grab first index, even if there are multiple.
			if(matched.Count != 0)
			{
				resourceReferences.mesh = (Mesh)matched[0];
			}
		}

		gameObjectPath = saveFormat.gameObjectPath;
		return resourceReferences;
#else
		gameObjectPath = "";
		return new ResourceReferences();
#endif
	}

	//Saves ResourceReferences to disk.
	private void SaveToDisk()
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
	private bool LoadFromDisk()
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
}
