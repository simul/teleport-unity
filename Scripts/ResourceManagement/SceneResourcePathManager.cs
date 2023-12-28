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
	/// A class to store the connection between Unity Assets and Teleport asset paths.
	/// </summary>
	public class SceneResourcePathManager : MonoBehaviour, ISerializationCallbackReceiver
	{
		//Used to serialise dictionary, so it can be refilled when the object is deserialised.
		[SerializeField, HideInInspector] UnityEngine.Object[] sceneResourcePaths_keys;
		[SerializeField, HideInInspector] string[] sceneResourcePaths_values;

		[NonSerialized]
		static Dictionary<Scene,SceneResourcePathManager> sceneResourcePathManagers=new Dictionary<Scene, SceneResourcePathManager> ();

		//STATIC FUNCTIONS
		public static Dictionary<Scene, SceneResourcePathManager> GetSceneResourcePathManagers()
		{
			return sceneResourcePathManagers;
		}
		static public SceneResourcePathManager GetSceneResourcePathManager(Scene scene)
		{
			if(scene==null|| scene.path==null)
				return null;
			if(sceneResourcePathManagers.TryGetValue(scene,out SceneResourcePathManager res))
			{
				if(res!=null)
					return res;
			}
			SceneResourcePathManager sceneResourcePathManager=null;
			var objs = scene.GetRootGameObjects();
			foreach (var o in objs)
			{
				SceneResourcePathManager m = o.GetComponentInChildren<SceneResourcePathManager>();
				if (m)
				{
					sceneResourcePathManager=m;
					break;
				}
			}
			if (sceneResourcePathManager == null)
				sceneResourcePathManager = FindObjectOfType<SceneResourcePathManager>();
			if (sceneResourcePathManager == null)
			{
				var tempObject = new GameObject("SceneResourcePathManager");
				//Add Components
				tempObject.AddComponent<SceneResourcePathManager>();
				sceneResourcePathManager = tempObject.GetComponent<SceneResourcePathManager>();
			}
			sceneResourcePathManagers[scene]= sceneResourcePathManager;
			return sceneResourcePathManager;
		}

		[NonSerialized]
		private Dictionary<UnityEngine.Object,string> sceneResourcePaths=new Dictionary<UnityEngine.Object, string>();

		public Dictionary<UnityEngine.Object, string> GetResourcePathMap()
		{
			return sceneResourcePaths;
		}
		static public void ClearAll()
		{
			// At least get the one in the current scene.
			GetSceneResourcePathManager(SceneManager.GetActiveScene());
			foreach (var s in sceneResourcePathManagers)
			{
				s.Value.Clear();
			}
		}

		public void Clear()
		{
			sceneResourcePaths.Clear();
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
		public void CheckForDuplicates()
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				for(int j=i+1;j< sceneResourcePaths_keys.Length;j++)
				{
					if (sceneResourcePaths_values[i] == sceneResourcePaths_values[j])
					{
						sceneResourcePaths.Remove(sceneResourcePaths_keys[j]);
						Debug.LogWarning($"Removed duplicate resource path {sceneResourcePaths_values[j]} for {sceneResourcePaths_keys[j].name} and {sceneResourcePaths_keys[j].name}.");
					}
				}
			}
		}
		static public string StandardizePath(string file_name,string path_root)
		{
			string p = file_name;
			p=p.Replace(" ","___");
			p=p.Replace('\\','/');
			if(path_root.Length>0)
				p=p.Replace(path_root, "" );
			//int last_dot_pos = p.LastIndexOf('.');
			//if(last_dot_pos>0&&last_dot_pos < p.Length)
			//	p=p.Substring(0,last_dot_pos);
			return p;
		}
		public void SetResourcePath(UnityEngine.Object o,string p)
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				if (sceneResourcePaths_keys[i]!=o&&sceneResourcePaths_values[i] == p)
				{
					if(sceneResourcePaths_keys[i]==null)
					{
						// already have this path, but for a deleted object. Let's remove that. But although C# is able to leave null keys lying around,
						// it WON'T let you remove them!
						//sceneResourcePaths.Remove(null);
						sceneResourcePaths_values[i]="";
					}
					else
					{
						Debug.LogError($"Trying to add duplicate resource path {p} for {o.GetType().Name} {o.name}, but already present for {sceneResourcePaths_keys[i].GetType().Name}  {sceneResourcePaths_keys[i].name}.");
						return;
					}
				}
			}
			sceneResourcePaths[o]= StandardizePath(p,"");
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
		public string GetResourcePath(UnityEngine.Object o)
		{
			string path="";
			if(sceneResourcePaths ==null)
				sceneResourcePaths = new Dictionary<UnityEngine.Object, string>();
			sceneResourcePaths.TryGetValue(o,out path);
			return path;
		}
		///INHERITED FUNCTIONS

		public void OnBeforeSerialize()
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				if(sceneResourcePaths_keys[i]==null)
				{ 
					var keys= sceneResourcePaths_keys.ToList();
					var vals = sceneResourcePaths_values.ToList();
					keys.RemoveAt(i);
					vals.RemoveAt(i);
					sceneResourcePaths_keys=keys.ToArray();
					sceneResourcePaths_values = vals.ToArray();
					i--;
				}
			}
		}

		public void OnAfterDeserialize()
		{
			if(sceneResourcePaths_keys!=null)
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				sceneResourcePaths[sceneResourcePaths_keys[i]] = sceneResourcePaths_values[i];
			}
		}

	}
}
