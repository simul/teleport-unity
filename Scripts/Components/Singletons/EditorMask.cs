using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace teleport
{
	[ExecuteInEditMode]
	//! A singleton class to ensure that objects have the correct rendering layer masks in-editor.
	public class EditorMask : MonoBehaviour
	{
		static EditorMask instance=null;
		public static EditorMask GetInstance()
		{
			// We only want one instance, so delete duplicates.
			if (instance == null)
			{
				for (int i = 0; i < SceneManager.sceneCount; i++)
				{
					var s=SceneManager.GetSceneAt(i);
					if(s==null||!s.isLoaded)
						continue;
					var objs = s.GetRootGameObjects();
					foreach (var o in objs)
					{
						var m = o.GetComponentInChildren<EditorMask>();
						if (m)
						{
							instance = m;
							return instance;
						}
					}
				}
				instance = FindObjectOfType<EditorMask>();
				if (instance == null)
				{
					var tempObject = new GameObject("EditorMask");
					//Add Components
					tempObject.AddComponent<EditorMask>();
					Initialize();
					instance = tempObject.GetComponent<EditorMask>();
				}
			}
			return instance;
		}
		static public void ResetAll()
        {
			if (Application.isPlaying)
				return;
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var objs = SceneManager.GetSceneAt(i).GetRootGameObjects();
				foreach (var o in objs)
				{
					teleport.Monitor.OverrideRenderingLayerMask(o, 0xFFFFFFFF,true);
				}
			}
			Initialize();
		}
		private void OnEnable()
		{
#if UNITY_EDITOR
			Selection.selectionChanged += ResetAll;
#endif
		}
		private void OnDisable()
		{
#if UNITY_EDITOR
			Selection.selectionChanged -= ResetAll;
#endif
		}
		static uint streamable_mask = (uint)1 << 31;
		static uint unstreamable_mask = (uint)1 << 30;
		static public void InitializeObject(GameObject o)
		{
			var streamableNode=o.GetComponent<StreamableNode>();
			if(streamableNode!=null)
			{
				teleport.Monitor.UnsetRenderingLayerMask(o, streamable_mask);
				teleport.Monitor.SetRenderingLayerMask(o, unstreamable_mask);
			}
			else
			{
				teleport.Monitor.SetRenderingLayerMask(o, streamable_mask);
				teleport.Monitor.UnsetRenderingLayerMask(o, unstreamable_mask);
			}
		}
		static public void Initialize()
		{
			if (Application.isPlaying)
				return;
			// first clear 31 for all objects.
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var s = SceneManager.GetSceneAt(i);
				UpdateMasks(s);
			}
		}
		static private void UpdateMasks(Scene scene)
		{
			uint streamable_mask = (uint)1 << 31;
			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			foreach (GameObject o in rootGameObjects)
			{
				teleport.Monitor.UnsetRenderingLayerMask(o, streamable_mask, true);
				teleport.Monitor.SetRenderingLayerMask(o, unstreamable_mask, true);
			}

			//Set the mask on all streamable objects.
			List<GameObject> teleportStreamableObjects = GeometrySource.GetGeometrySource().GetStreamableNodes(scene);
			foreach (GameObject o in teleportStreamableObjects)
			{
				teleport.Monitor.SetRenderingLayerMask(o, streamable_mask);
			}
			// If we're doing tags, set the mask on objects that will become streamable due to their tag.
			if (TagHandler.Instance != null)
			{
				GameObject[] taggedObjects = TagHandler.Instance.GetTaggedObjects(scene);
				foreach (GameObject o in taggedObjects)
				{
					teleport.Monitor.SetRenderingLayerMask(o, streamable_mask);
					teleport.Monitor.UnsetRenderingLayerMask(o, unstreamable_mask, true);
				}
			}
		}
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (Application.isPlaying)
				return;
			UpdateMasks(scene);
		}

		// Start is called before the first frame update
		void Start()
		{
		}
		// Update is called once per frame
		void Update()
		{
        
		}
	}
}
