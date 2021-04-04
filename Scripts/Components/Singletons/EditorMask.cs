using System.Collections;
using System.Collections.Generic;
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
					var objs = SceneManager.GetSceneAt(i).GetRootGameObjects();
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
					EditorMask.Initialize();
					instance = tempObject.GetComponent<EditorMask>();
				}
			}
			return instance;
		}
		static public void Initialize()
		{
			if (Application.isPlaying)
				return;
			uint streamable_mask = (uint)1 << 31;
			// first clear 31 for all objects.
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var objs = SceneManager.GetSceneAt(i).GetRootGameObjects();
				foreach (var o in objs)
					CasterMonitor.UnsetMaskRecursive(o, streamable_mask);
			}
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			List<GameObject> streamable = geometrySource.GetStreamableObjects();
			foreach (var o in streamable)
			{
				CasterMonitor.SetMaskRecursive(o, streamable_mask);
			}
		}
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (Application.isPlaying)
				return;
			uint streamable_mask = (uint)1 << 31;
			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			foreach (GameObject o in rootGameObjects)
			{
				CasterMonitor.UnsetMaskRecursive(o, streamable_mask);
			}

			//Add the Teleport_Streamable component to all streamable objects.
			List<GameObject> teleportStreamableObjects = GeometrySource.GetGeometrySource().GetStreamableObjects();
			foreach (GameObject o in teleportStreamableObjects)
			{
				CasterMonitor.SetMaskRecursive(o, streamable_mask);
			}
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
