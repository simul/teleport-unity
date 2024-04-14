using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static teleport.DistanceGeometryManagement;

namespace teleport
{
	class XComparer : IComparer<teleport.StreamableRoot>
	{
		public int Compare(teleport.StreamableRoot a, teleport.StreamableRoot b)
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
	class YComparer : IComparer<teleport.StreamableRoot>
	{
		public int Compare(teleport.StreamableRoot a, teleport.StreamableRoot b)
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
	class ZComparer : IComparer<teleport.StreamableRoot>
	{
		public int Compare(teleport.StreamableRoot a, teleport.StreamableRoot b)
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
		public class TrackedBounds
		{
			public TrackedBounds(teleport.StreamableRoot str, bool lwr)
			{
				streamable=str;
				lower_bounds=lwr;
			}
			public teleport.StreamableRoot streamable;
			public bool lower_bounds;
		}
		// Keep six ordered lists of all the teleport.StreamableRoots.
		// Each list represents the + or - extent of the given object (treating it as a sphere with a certain size).
		// for now, we consider all the objects to have fixed size 2 metres.
		static List<teleport.StreamableRoot> all_streamables = new List<teleport.StreamableRoot>();
		static List<TrackedBounds> X_bounds = new List<TrackedBounds>();
		static List<TrackedBounds> Y_bounds = new List<TrackedBounds>();
		static List<TrackedBounds> Z_bounds = new List<TrackedBounds>();
		private TeleportSettings teleportSettings = null;
		static  DistanceGeometryManagement()
		{
		}
		public void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			teleport.StreamableRoot[] streamables = FindObjectsByType<teleport.StreamableRoot>(FindObjectsSortMode.None);
			all_streamables=streamables.ToList();
			foreach(var streamable in streamables)
			{
				X_bounds.Add(new TrackedBounds(streamable,false));
				X_bounds.Add(new TrackedBounds(streamable, true));
				Y_bounds.Add(new TrackedBounds(streamable, false));
				Y_bounds.Add(new TrackedBounds(streamable, true));
				Z_bounds.Add(new TrackedBounds(streamable, false));
				Z_bounds.Add(new TrackedBounds(streamable, true));
			}
		}
		public static Vector3 GetBound(TrackedBounds trackedBounds)
		{
			Collider coll = trackedBounds.streamable.GetComponent<Collider>();
			Vector3 pos = trackedBounds.streamable.transform.position;
			if (coll)
			{
				if(trackedBounds.lower_bounds)
					pos = coll.bounds.min;
				else
					pos = coll.bounds.max;
			}
			else
			{
				MeshRenderer m = trackedBounds.streamable.GetComponent<MeshRenderer>();
				if (m)
				{
					if (trackedBounds.lower_bounds)
						pos = m.bounds.min;
					else
						pos = m.bounds.max;
				}
			}
			return pos;
		}
		public static Vector3 Max(teleport.StreamableRoot streamable)
		{
			Collider coll = streamable.GetComponent<Collider>();
			Vector3 pos = streamable.transform.position;
			if (coll)
			{
				pos = coll.bounds.max;
			}
			else
			{
				MeshRenderer m = streamable.GetComponent<MeshRenderer>();
				if (m)
					pos = m.bounds.max;
			}
			return pos;
		}
		void Update()
		{
			// X_minus should be in the order of the left-most edge of the bounds of each streamable.
			X_bounds.Sort(delegate (TrackedBounds a, TrackedBounds b)
			{
				float A = 0.0F;
				float B = 0.0F;
				A=GetBound(a).x;
				B = GetBound(b).x;
				return Comparer<float>.Default.Compare(A,B);
			});
			// X_minus should be in the order of the left-most edge of the bounds of each streamable.
			Y_bounds.Sort(delegate (TrackedBounds a, TrackedBounds b)
			{
				float A = 0.0F;
				float B = 0.0F;
				A = GetBound(a).y;
				B = GetBound(b).y;
				return Comparer<float>.Default.Compare(A, B);
			});
			Z_bounds.Sort(delegate (TrackedBounds a, TrackedBounds b)
			{
				float A = 0.0F;
				float B = 0.0F;
				A = GetBound(a).z;
				B = GetBound(b).z;
				return Comparer<float>.Default.Compare(A, B);
			});
		}
		HashSet<teleport.StreamableRoot> innerStreamables = new HashSet<teleport.StreamableRoot>();
		HashSet<teleport.StreamableRoot> outerStreamables = new HashSet<teleport.StreamableRoot>();
		int inner_overlap_count = 0;
		//int outer_overlap_count = 0;
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
			var currentStreamables=session.GeometryStreamingService.GetCurrentStreamables();
			var layersToStream = teleportSettings.LayersToStream;
			// Detect changes in geometry that needs to be streamed to the client.
			// each client session component should maintain a list of the TeleportStreamable (root) objects it is tracking.

			// We consider a box centred on the session root, and of fixed size in X, Y and Z.
			// This box at any time impinges on a subset of the Streamables.
			// Using our lists we know the group that it impinges on in the X, Y and Z directions.
			// The streamables that are present in all three lists are the ones we should stream.

			if (layersToStream != 0)
			{
				Vector3 position = session.head.transform.position;
				float R0 = teleportSettings.serverSettings.detectionSphereRadius;
				float R1 = R0 + teleportSettings.serverSettings.detectionSphereBufferDistance;

				int first_x_index = -1, last_x_index =  - 1;
				Vector3 pos_min= position-new Vector3(R0,R0,R0);
				Vector3 pos_max = position+ new Vector3(R0, R0, R0);
				// What's the right-most box that's still left of the session's right edge?
				for (int i = 0; i < X_bounds.Count; i++)
				{
					if(X_bounds[i].streamable.transform.position.x>pos_max.x)
					{
						last_x_index = i;
						break;
					}
				}
				if(first_x_index>=0&&last_x_index>=0)
				{

				}
				gainedStreamables.Clear();
				for (int i = 0; i < all_streamables.Count; i++)
				{
					var streamable= all_streamables[i];
					GameObject g = streamable.gameObject;
					if (!g)
						continue;
					float distance = (g.transform.position - position).magnitude;
					float objectRadius= streamable.GetBounds().size.magnitude;
					if (distance < R0+objectRadius)
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
					float objectRadius = streamable.GetBounds().size.magnitude;
					if (distance> R1 + objectRadius)
						lostStreamables.Add(streamable);
				}
			}
		}
		public bool CheckRootCanStream(teleport.StreamableRoot r)
		{ 
			return true;
		}
	}
}