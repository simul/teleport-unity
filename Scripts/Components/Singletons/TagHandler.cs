using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace teleport
{
	/// <summary>
	/// A singleton component which manages object streamability based on the use of tags.
	/// The TagHandler is created automatically by Monitor on the same GameObject. It is a singleton,
	/// only one should exist in a scene. Do not create a TagHandler, the Monitor will do this if TeleportSettings.TagToStream is not empty.
	/// TagHandler will ensure that any object tagged with the tag given by TeleportSettings.TagToStream will be streamable.
	/// </summary>
	public class  TagHandler: MonoBehaviour
	{
		private static TagHandler instance; //There should only be one teleport.Monitor instance at a time.

		public static TagHandler Instance
		{
			get
			{
				// We only want one instance, so delete duplicates.
				if (instance == null)
				{
					TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
					if (teleportSettings.TagToStream.Length==0)
						return null;
					instance = FindObjectOfType<teleport.TagHandler>();
					if (instance == null)
					{
						teleport.Monitor monitor= teleport.Monitor.Instance;
						instance=monitor.GetComponent<TagHandler>();
						if(instance == null)
						{
							instance =monitor.gameObject.AddComponent<teleport.TagHandler>();
						}
					}
				}
				return instance;
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
		public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			string streamingTag = TeleportSettings.GetOrCreateSettings().TagToStream;
			if(streamingTag.Length==0)
				return;
			//Add the teleport.StreamableNode to all objects that have the tag
			var objs = scene.GetRootGameObjects();
			foreach (var o in objs)
			{
				UnityEngine.Transform[] trs = o.GetComponentsInChildren<UnityEngine.Transform>();
				foreach (var t in trs)
				{
					if(t.gameObject.CompareTag(streamingTag))
					{ 
						if (t.gameObject.GetComponent<teleport.StreamableNode>() == null)
							t.gameObject.AddComponent<teleport.StreamableNode>();
					}
				}
			}
			foreach (var o in objs)
			{
				teleport.StreamableNode[] str = o.GetComponentsInChildren<teleport.StreamableNode>();
				foreach (var s in str)
				{
					if (s.gameObject.GetComponent<teleport.StreamableNode>() != null
						&&s.gameObject.GetComponent<teleport.StreamableRoot>()==null)
					{
						if(s.gameObject.transform.parent==null||s.gameObject.GetComponentsInParent<teleport.StreamableRoot>().Length==0)
						{
							s.gameObject.AddComponent<teleport.StreamableRoot>();
						}
					}
				}
			}
		}
		/// <summary>
		/// Get the objects that have the current TeleportSettings.TagToStream.
		/// When running, these objects will be streamable.
		/// </summary>
		public GameObject [] GetTaggedObjects(Scene scene)
		{
			List<GameObject> tagged = new List<GameObject>();
			string streamingTag = TeleportSettings.GetOrCreateSettings().TagToStream;
			if (streamingTag.Length != 0)
			{
				var objs = scene.GetRootGameObjects();
				foreach (var o in objs)
				{
					UnityEngine.Transform[] trs = o.GetComponentsInChildren<UnityEngine.Transform>();
					foreach (var t in trs)
					{
						if (t.gameObject.CompareTag(streamingTag))
						{
							tagged.Add(t.gameObject);
						}
					}
				}
			}
			return tagged.ToArray();
		}

			//If the passed collision layer is streamed.
			static public bool IsCollisionLayerStreamed(int layer)
		{
			var settings = TeleportSettings.GetOrCreateSettings();
			if (settings.LayersToStream.value == 0)
				return true;
			return (settings.LayersToStream & (1 << layer)) != 0;
		}
		//If the GameObject has been marked correctly to be streamed; i.e. on streamed collision layer and has the correct tag.
		static public bool IsGameObjectMarkedForStreaming(GameObject gameObject)
		{
			string streamingTag = TeleportSettings.GetOrCreateSettings().TagToStream;
			return (streamingTag.Length == 0 || gameObject.CompareTag(streamingTag)) && IsCollisionLayerStreamed(gameObject.layer);
		}
#if REMOVE_UNTAGGED
		private void StopStreamingUntaggedStreamables()
		{
			var geometrySource = GeometrySource.GetGeometrySource();
			for (int i = streamedHierarchies.Count - 1; i >= 0; i--)
			{
				teleport.StreamableRoot streamable = streamedHierarchies[i];
				if (!geometrySource.IsGameObjectMarkedForStreaming(streamable.gameObject))
				{
					ClientStreamableTracking tracking = GetTracking(streamable);
					if ((tracking.streaming_reason & (uint)StreamingReason.NEARBY) != 0)
					{
						StopStreaming(streamable, StreamingReason.NEARBY);
					}
				}
			}
		}
#endif
	}

}