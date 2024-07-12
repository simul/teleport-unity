using System.Collections;
using System.Collections.Generic;
using teleport;
using UnityEditor;
using UnityEngine;

namespace teleport
{
	//! An example geometry management class based on collision. StreamableRoots must have a collider.
	//! We detect collision between a sphere of radius teleportSettings.serverSettings.detectionSphereRadius and the colliders of the 
	//! streamable roots.
	public class CollisionGeometryManagement : MonoBehaviour, IStreamedGeometryManagement
	{
		private TeleportSettings teleportSettings = null;
		public CollisionGeometryManagement()
		{
		}
		public void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
		}
		Collider[] innerOverlappingColliders = new Collider[10];
		Collider[] outerOverlappingColliders = new Collider[10];
		HashSet<teleport.StreamableRoot> innerStreamables = new HashSet<teleport.StreamableRoot>();
		HashSet<teleport.StreamableRoot> outerStreamables = new HashSet<teleport.StreamableRoot>();
		int inner_overlap_count = 0;
		int outer_overlap_count = 0;
		// Update is called once per frame
		public void UpdateStreamedGeometry(Teleport_SessionComponent session,ref List<teleport.StreamableRoot> gainedStreamables
		,ref List<teleport.StreamableRoot> lostStreamables, List<teleport.StreamableRoot> streamedHierarchies)
		{
			if (!session.IsConnected())
				return;
			if (session.GetClientID() == 0)
			{
				TeleportLog.LogErrorOnce("Client ID is zero.");
				return;
			}
			var layersToStream = teleportSettings.LayersToStream;
			//Detect changes in geometry that needs to be streamed to the client.
			// each client session component should maintain a list of the TeleportStreamable (root) objects it is
			// tracking. Perhaps count how many colliders it is impinging for each root.
			// The session can use OnTriggerEnter and OnTriggerExit to update this list.
			if (layersToStream != 0)
			{
				Vector3 position = session.head.transform.position;
				float R0 = teleportSettings.serverSettings.detectionSphereRadius;
				float R1 = R0 + teleportSettings.serverSettings.detectionSphereBufferDistance;
				inner_overlap_count = Physics.OverlapSphereNonAlloc(position, R0, innerOverlappingColliders, teleportSettings.LayersToStream);
				if (inner_overlap_count >= innerOverlappingColliders.Length)
				{
					innerOverlappingColliders = new Collider[inner_overlap_count * 2];
					inner_overlap_count = Physics.OverlapSphereNonAlloc(position, R0, innerOverlappingColliders, teleportSettings.LayersToStream);
				}
				outer_overlap_count = Physics.OverlapSphereNonAlloc(position, R1, outerOverlappingColliders, teleportSettings.LayersToStream);
				if (outer_overlap_count >= outerOverlappingColliders.Length)
				{
					outerOverlappingColliders = new Collider[outer_overlap_count * 2];
					outer_overlap_count = Physics.OverlapSphereNonAlloc(position, R0, outerOverlappingColliders, teleportSettings.LayersToStream);
				}
				gainedStreamables = new List<teleport.StreamableRoot>();
				for (int i = 0; i < inner_overlap_count; i++)
				{
					GameObject g = innerOverlappingColliders[i].gameObject;
					if (!g)
						continue;
					if (!innerOverlappingColliders[i].enabled)
						continue;
					var streamable = g.GetComponentInParent<teleport.StreamableRoot>();
					if (!streamable)
						continue;
					if (streamable.priority < teleportSettings.defaultMinimumNodePriority)
						continue;
					if (streamedHierarchies.Contains(streamable))
						continue;
					gainedStreamables.Add(streamable);
				}
				HashSet<teleport.StreamableRoot> keptOuterStreamables = new HashSet<teleport.StreamableRoot>();
				for (int i = 0; i < outer_overlap_count; i++)
				{
					GameObject g = outerOverlappingColliders[i].gameObject;
					if (!g)
						continue;
					if (!outerOverlappingColliders[i].enabled)
						continue;
					var streamable = g.GetComponentInParent<teleport.StreamableRoot>();
					if (!streamable)
						continue;
					if (outerStreamables.Contains(streamable))
						continue;
					keptOuterStreamables.Add(streamable);
				}
				foreach (var s in outerStreamables)
				{
					if (!keptOuterStreamables.Contains(s))
					{
						outerStreamables.Remove(s);
						lostStreamables.Add(s);
					}
				}
			}
		}
		string lastWarning="";
		public string GetLastWarning()
		{
			return lastWarning;
		}
		public bool CheckRootCanStream(teleport.StreamableRoot r)
		{
			var c=r.GetComponent<Collider>();
			if(c==null)
			{
				lastWarning = r.name + " cannot stream because it has no collider.";
				return false;
			}
			if ( !c.enabled)
			{
				lastWarning = r.name + " cannot stream because its collider is not enabled.";
				return false;
			}
			return true;
		}
	}
}