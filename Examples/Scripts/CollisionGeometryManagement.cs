using System.Collections;
using System.Collections.Generic;
using teleport;
using UnityEditor;
using UnityEngine;

namespace teleport
{
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
		HashSet<Teleport_Streamable> innerStreamables = new HashSet<Teleport_Streamable>();
		HashSet<Teleport_Streamable> outerStreamables = new HashSet<Teleport_Streamable>();
		int inner_overlap_count = 0;
		int outer_overlap_count = 0;
		// Update is called once per frame
		public void UpdateStreamedGeometry(Teleport_SessionComponent session,List<Teleport_Streamable> gainedStreamables, List<Teleport_Streamable> lostStreamables)
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
				gainedStreamables = new List<Teleport_Streamable>();
				for (int i = 0; i < inner_overlap_count; i++)
				{
					GameObject g = innerOverlappingColliders[i].gameObject;
					if (!g)
						continue;
					if (!innerOverlappingColliders[i].enabled)
						continue;
					var streamable = g.GetComponentInParent<Teleport_Streamable>();
					if (!streamable)
						continue;
					if (innerStreamables.Contains(streamable))
						continue;
					innerStreamables.Add(streamable);
					gainedStreamables.Add(streamable);
				}
				lostStreamables = new List<Teleport_Streamable>();
				HashSet<Teleport_Streamable> keptOuterStreamables = new HashSet<Teleport_Streamable>();
				for (int i = 0; i < outer_overlap_count; i++)
				{
					GameObject g = outerOverlappingColliders[i].gameObject;
					if (!g)
						continue;
					if (!outerOverlappingColliders[i].enabled)
						continue;
					var streamable = g.GetComponentInParent<Teleport_Streamable>();
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
	}
}