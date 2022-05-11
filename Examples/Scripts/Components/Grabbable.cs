using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	using InputID = System.UInt16;
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
			TeleportSettings settings=TeleportSettings.GetOrCreateSettings();
			LeftGrabInputId = settings.FindInput("Left Trigger Click");
			RightGrabInputId = settings.FindInput("Right Trigger Click");
		}

		// Update is called once per frame
		void Update()
		{
        
		}
		public uid holderClient=0;
		System.UInt16 LeftGrabInputId =0;
		System.UInt16 RightGrabInputId = 0;
		GameObject formerParent=null;
		Vector3 oldRelativePosition=new Vector3();
		Quaternion oldRelativeRotation=new Quaternion();
		Teleport_Controller nearController = null;

		public void Grab(Input input, InputID inputId)
		{
			if(holderClient != 0)
				return;
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			holderClient = session.GetClientID();
			if(holderClient==0)
				return;
			Debug.Log("Grabbed " + topParent + " with " + nearController.gameObject);
			// For now, we do the reparenting only client-side on that one client.
			// I expect this to change so that all clients + server recognize the new structure.
			formerParent= topParent.transform.parent?topParent.transform.parent.gameObject:null;
			teleport.Monitor casterMonitor = teleport.Monitor.Instance;
			oldRelativePosition= topParent.transform.localPosition;
			oldRelativeRotation= topParent.transform.localRotation;
			Transform grabbableT	= gameObject.transform;
			Transform childT		= topParent.transform;
			Quaternion relativeRotation = Quaternion.Inverse(grabbableT.rotation) * childT.rotation;
			Vector3 relativePosition= Quaternion.Inverse(grabbableT.rotation) *(childT.position- grabbableT.position);
			teleport.Monitor.Instance.ReparentNode(topParent, nearController.gameObject, relativePosition, relativeRotation);
			
			session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			session.input.RemoveDelegate(inputId, Grab, InputEventType.Release);
			session.input.AddDelegate(inputId, Drop, InputEventType.Release);
		}
		public void Drop(Input input, InputID inputId)
		{
			if (holderClient == 0)
				return;
			Debug.Log("Dropped " + topParent);
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			session.input.RemoveDelegate(inputId, Drop, InputEventType.Release);
			teleport.Monitor.Instance.ReparentNode(topParent, formerParent, oldRelativePosition, oldRelativeRotation);
			holderClient=0;
		}
		// This occurs when a collider impinges on the Grabbable.
		private void OnTriggerEnter(Collider other)
		{
			if(holderClient!=0)
				return;
			Teleport_Controller controller= other.GetComponentInChildren<Teleport_Controller>();
			if(!controller)
				return;
			if (nearController!=null)
				return;
			Debug.Log("Detected collision between " + gameObject.name + " and " + other.name);
			Teleport_SessionComponent session= controller.session;
			if (session!=null&&session.GeometryStreamingService!=null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, true);
			if(controller.poseRegexPath.Contains("left"))
				session.input.AddDelegate(LeftGrabInputId, Grab, InputEventType.Release);
			if (controller.poseRegexPath.Contains("right"))
				session.input.AddDelegate(RightGrabInputId, Grab, InputEventType.Release);
			nearController =controller;
		}
		void OnTriggerStay(Collider other)
		{
		//	Debug.Log(gameObject.name + " and " + other.name + " are still colliding");
		}

		void OnTriggerExit(Collider other)
		{
			Teleport_Controller controller = other.GetComponentInChildren<Teleport_Controller>();
			if (!controller||controller!=nearController)
				return;
			nearController=null;
			if (holderClient != 0)
				return;
			Debug.Log(gameObject.name + " and " + other.name + " are no longer colliding");
			Teleport_SessionComponent session = controller.session;
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			if (controller.poseRegexPath.Contains("left"))
				session.input.RemoveDelegate(LeftGrabInputId, Grab, InputEventType.Release);
			if (controller.poseRegexPath.Contains("right"))
				session.input.RemoveDelegate(RightGrabInputId, Grab, InputEventType.Release);
		}

	}
}