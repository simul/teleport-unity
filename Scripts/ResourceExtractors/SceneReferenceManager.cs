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
	public class SceneReferenceManager : MonoBehaviour
	{
		private static Dictionary<Scene,SceneReferenceManager> sceneReferenceManagers=new Dictionary<Scene, SceneReferenceManager>();

		//STATIC FUNCTIONS
		public static Dictionary<Scene, SceneReferenceManager>  GetSceneReferenceManagers()
		{
			return sceneReferenceManagers;
		}
        public static Dictionary<GameObject, Mesh> GetObjectMeshMap(Scene scene)
		{
            Dictionary<GameObject, Mesh > mp= new Dictionary<GameObject, Mesh>();
			foreach (var g in scene.GetRootGameObjects())
			{
				var mt = g.GetComponentsInChildren<MeshTracker>();
				foreach(var m in mt)
					mp[m.gameObject] = m.mesh;
			}
			return mp;
        }
        public static SceneReferenceManager GetSceneReferenceManager(Scene scene)
		{
			if(scene==null||scene.path==null)
				return null;
			if (sceneReferenceManagers.TryGetValue(scene, out SceneReferenceManager res))
			{
				if (res != null)
					return res;
			}
			SceneReferenceManager sceneReferenceManager = null;
			var objs = scene.GetRootGameObjects();
			foreach (var o in objs)
			{
				SceneReferenceManager m = o.GetComponentInChildren<SceneReferenceManager>();
				if (m)
				{
					sceneReferenceManager = m;
					break;
				}
			}
			if (sceneReferenceManager == null)
				sceneReferenceManager = FindObjectOfType<SceneReferenceManager>();
			if (sceneReferenceManager == null)
			{
				var tempObject = new GameObject("SceneReferenceManager");
				//Add Components
				tempObject.AddComponent<SceneReferenceManager>();
				sceneReferenceManager = tempObject.GetComponent<SceneReferenceManager>();
			}
			sceneReferenceManagers[scene] = sceneReferenceManager;
			
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			return sceneReferenceManager;
		}
		static public void OnSceneUnloaded(Scene scene)
		{
			sceneReferenceManagers.Remove(scene);
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
		}
		///PUBLIC FUNCTIONS

		//Add GameObject to SceneReferenceManager; extracting references to resources it uses.
		public MeshTracker AddGameObject(GameObject gameObject, string gameObjectPath = null)
		{
			if(!gameObject)
			{
				Debug.LogError("Passed null GameObject to SceneReferenceManager!");
            }

            //Retrieve path of GameObject, if gameObjectPath is null. X??Y is equivalent to X?X:Y
            gameObjectPath = gameObjectPath ?? GetGameObjectPath(gameObject);
			MeshTracker meshTracker= gameObject.GetComponent<MeshTracker>();
            //Only return what we have in the dictionary, if we are in play-mode; we want to re-extract if we are in the editor to update to any changes made.
            if (Application.isPlaying && meshTracker)
			{
				return meshTracker;
			}
			SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
			if (skinnedMeshRenderer!=null)
			{
				if (meshTracker == null)
				{
					meshTracker = gameObject.AddComponent<MeshTracker>();
				}
			}
			else
			{
				List<MeshTracker> meshTrackers = gameObject.GetComponents<MeshTracker>().ToList();
				MeshFilter[] meshFilters = gameObject.GetComponents<MeshFilter>();
				while(meshFilters.Length< meshTrackers.Count)
				{
					UnityEngine.Object.DestroyImmediate(meshTrackers[meshTrackers.Count-1]);
					meshTrackers.RemoveAt(meshTrackers.Count - 1);
				}
				while (meshTrackers.Count < meshFilters.Length)
				{
					gameObject.AddComponent<MeshTracker>();
					meshTrackers = gameObject.GetComponents<MeshTracker>().ToList();
				}
				if (meshTracker == null)
				{
					if (meshTrackers.Count > 0)
						meshTracker = meshTrackers[0];
				}
			}
			if (meshTracker == null)
				return null;
			meshTracker.mesh = GetMesh(gameObject);
			var sceneResourcePathManager = SceneResourcePathManager.GetSceneResourcePathManager(gameObject.scene);
			string resourcePath= sceneResourcePathManager.GetResourcePath(meshTracker.mesh);
			if(resourcePath=="")
				resourcePath = SceneResourcePathManager.GetNonAssetResourcePath(meshTracker.mesh,gameObject);
			meshTracker.resourcePath= resourcePath;
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(gameObject);
			UnityEditor.EditorUtility.SetDirty(meshTracker);
#endif
			return meshTracker;
		}

		//Get references to mesh used by GameObject. Will add the GameObject to the SceneReferenceManager, if it has not already been added.
		//Returns null if the GameObject does not use a mesh.
		public Mesh GetMeshFromGameObject(GameObject gameObject)
		{
			string gameObjectPath = GetGameObjectPath(gameObject);
            MeshTracker meshTracker = gameObject.GetComponent<MeshTracker>();
            // We want to re-extract the references if we are not in play-mode; i.e. we want to update to changes made in the editor.
            if (!Application.isPlaying || !meshTracker)
			{
                AddGameObject(gameObject, gameObjectPath);
            }
            meshTracker = gameObject.GetComponent<MeshTracker>();
			if(!meshTracker)
				return null;
            return meshTracker.mesh;
		}
		//Returns path of GameObject in scene hierarchy.
		static public string GetGameObjectPath(GameObject gameObject)
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
		static private Mesh GetMesh(GameObject gameObject)
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

	}
}
