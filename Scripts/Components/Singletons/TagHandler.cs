using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	/// <summary>
	/// A singleton component which manages object streamability based on the use of tags.
	/// The TagHandler is created automatically by Monitor on the same GameObject. It is a singleton,
	/// only one should exist in a scene. Do not create a TagHandler, the Monitor will do this if TeleportSettings.TagToStream is not empty.
	/// TagHandler will ensure that any object tagged with the tag given by TeleportSettings.TagToStream will be streamable.
	/// </summary>
	public class TagHandler : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {

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