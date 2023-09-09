using System.Collections;
using System.Collections.Generic;
using System.Linq;
using teleport;
using UnityEditor;
using UnityEngine;

namespace teleport
{
	class XComparer : IComparer<Teleport_Streamable>
	{
		public int Compare(Teleport_Streamable a, Teleport_Streamable b)
		{
			if (a == null || b == null)
				return 0;
			if (a.gameObject.transform.position.x > b.gameObject.transform.position.x)
				return 1;
			if (a.gameObject.transform.position.x < b.gameObject.transform.position.x)
				return -1;
			return 0;
		}
	}
	class YComparer : IComparer<Teleport_Streamable>
	{
		public int Compare(Teleport_Streamable a, Teleport_Streamable b)
		{
			if (a == null || b == null)
				return 0;
			if (a.gameObject.transform.position.y > b.gameObject.transform.position.y)
				return 1;
			if (a.gameObject.transform.position.y < b.gameObject.transform.position.y)
				return -1;
			return 0;
		}
	}
	class ZComparer : IComparer<Teleport_Streamable>
	{
		public int Compare(Teleport_Streamable a, Teleport_Streamable b)
		{
			if (a == null || b == null)
				return 0;
			if (a.gameObject.transform.position.z > b.gameObject.transform.position.z)
				return 1;
			if (a.gameObject.transform.position.z < b.gameObject.transform.position.z)
				return -1;
			return 0;
		}
	}

	public class DistanceGeometryManagement : MonoBehaviour, IStreamedGeometryManagement
	{
		// Keep six ordered lists of all the Teleport_Streamables.
		// Each list represents the + or - extent of the given object (treating it as a sphere with a certain size).
		// for now, we consider all the objects to have fixed size 2 metres.
		static List<Teleport_Streamable> X_minus = new List<Teleport_Streamable>();
		static List<Teleport_Streamable> Y_minus = new List<Teleport_Streamable>();
		static List<Teleport_Streamable> Z_minus = new List<Teleport_Streamable>();
		static List<Teleport_Streamable> X_plus = new List<Teleport_Streamable>();
		static List<Teleport_Streamable> Y_plus = new List<Teleport_Streamable>();
		static List<Teleport_Streamable> Z_plus = new List<Teleport_Streamable>();
		private TeleportSettings teleportSettings = null;
		static  DistanceGeometryManagement()
		{
		}
		public void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			Teleport_Streamable[] streamables = FindObjectsByType<Teleport_Streamable>(FindObjectsSortMode.None);
			X_minus = streamables.ToList<Teleport_Streamable>();
			Y_minus = X_minus;
			Z_minus = X_minus;
			X_plus = X_minus;
			Y_plus = X_minus;
			Z_plus = X_minus;
		}

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
			var currentStreamables=session.GeometryStreamingService.GetCurrentStreamables();
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
				gainedStreamables.Clear();
				for (int i = 0; i < X_minus.Count; i++)
				{
					var streamable= X_minus[i];
					GameObject g = streamable.gameObject;
					if (!g)
						continue;
					float distance = (g.transform.position - position).magnitude;
					if (distance < R0)
					{
						if (!currentStreamables.Contains(streamable))
						{
							gainedStreamables.Add(streamable);
						}
					}
				}
				lostStreamables.Clear();
				for (int i = 0; i < currentStreamables.Count; i++)
				{
					var streamable = currentStreamables[i];
					GameObject g = streamable.gameObject;
					if (!g)
						continue;
					float distance=(g.transform.position-position).magnitude;
					if(distance> R1)
						lostStreamables.Add(streamable);
				}
			}
		}
	}
}