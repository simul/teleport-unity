using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	/// <summary>
	/// An example component to make a parent object grabbable by a Teleport client, using Teleport_Controller.
	/// The transform of the Grabbable maps to the controller position when the object is held.
	/// The object that is held will be the highest parent of the Grabbable.
	/// </summary>
	public class Grabbable : MonoBehaviour
	{
		GameObject topParent=null;
		// Start is called before the first frame update
		void Start()
		{
			Transform t=transform;
			while(t.parent!=null)
				t= t.parent;
			topParent=t.gameObject;
		}

		// Update is called once per frame
		void Update()
		{
        
		}
		public uid holderClient=0;

		public void Grab(Teleport_Controller controller)
		{
			if(holderClient != 0)
				return;
			Debug.Log("Grabbed " + topParent + " with " + controller.gameObject);
			// For now, we do the reparenting only client-side on that one client.
			// I expect this to change so that all clients + server recognize the new structure.
			teleport.Monitor casterMonitor = teleport.Monitor.Instance;
			Transform grabbableT = gameObject.transform;
			Transform childT	= topParent.transform;
			Quaternion relativeRotation = Quaternion.Inverse(grabbableT.rotation) * childT.rotation;
			Vector3 relativePosition= Quaternion.Inverse(grabbableT.rotation) *(childT.position- grabbableT.position);
			teleport.Monitor.Instance.ReparentNode(topParent, controller.gameObject, relativePosition, relativeRotation);
			Teleport_SessionComponent session = controller.session;
			session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
		}
		public void Drop()
		{
			if (holderClient == 0)
				return;
			Debug.Log("Dropped " + topParent);
			// For now, we do the reparenting only client-side on that one client.
			// I expect this to change so that all clients + server recognize the new structure.
		//	controller.session.GeometryStreamingService.ReparentNode(topParent, controller.gameObject);
		}


		private void OnTriggerEnter(Collider other)
		{
			if(holderClient!=0)
				return;
			Teleport_Controller controller= other.GetComponentInChildren<Teleport_Controller>();
			if(!controller)
				return;
			Debug.Log("Detected collision between " + gameObject.name + " and " + other.name);
			Teleport_SessionComponent session= controller.session;
			if (session!=null&&session.GeometryStreamingService!=null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, true);
			controller.triggerReleaseDelegates+=Grab;
		}
		void OnTriggerStay(Collider other)
		{
		//	Debug.Log(gameObject.name + " and " + other.name + " are still colliding");
		}

		void OnTriggerExit(Collider other)
		{
			if (holderClient != 0)
				return;
			Teleport_Controller controller = other.GetComponentInChildren<Teleport_Controller>();
			if (!controller)
				return;
			Debug.Log(gameObject.name + " and " + other.name + " are no longer colliding");
			Teleport_SessionComponent session = controller.session;
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			controller.triggerReleaseDelegates -= Grab;
		}



	}
}