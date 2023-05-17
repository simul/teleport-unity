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
			while(t!=null&&t.GetComponent<Teleport_Streamable>()==null)
				t= t.parent;
			topParent=t.gameObject;
			TeleportSettings settings=TeleportSettings.GetOrCreateSettings();
			LeftGrabInputIds = settings.FindInputsByName("Left Grip Click");
			RightGrabInputIds = settings.FindInputsByName("Right Grip Click");
			LeftMouseClickInputIds = settings.FindInputsByName("Right Mouse Click"); 
		}

		// Update is called once per frame
		void Update()
		{
		
		}
		public uid holderClient=0;
		System.UInt16 [] LeftGrabInputIds ={ };
		System.UInt16 [] RightGrabInputIds = { };
		System.UInt16 [] LeftMouseClickInputIds = { };
		GameObject formerParent=null;
		Vector3 oldRelativePosition=new Vector3();
		Quaternion oldRelativeRotation=new Quaternion();
		Teleport_Controller nearController = null;

		public void Grab(Input input, InputID inputId)
		{
			//Debug.Log("Grab");
			if (holderClient != 0)
				return;
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			holderClient = session.GetClientID();
			if(holderClient==0)
				return;
			//Debug.Log("Grabbed " + topParent + " with " + nearController.gameObject);
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
			teleport.Monitor.Instance.ReparentNode(topParent, nearController.gameObject, relativePosition, relativeRotation, false);
			
			session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			session.input.RemoveDelegate(inputId, Grab, InputEventType.Press);
		}
		public void Drop(Input input, InputID inputId)
		{
			if (holderClient == 0)
				return;
			if(dropGrabbableHere==null)
				return;
			Transform t = dropGrabbableHere.transform;
			// Either parent from a streamable, or null.
			while (t != null && t.GetComponent<Teleport_Streamable>() == null)
				t = t.parent;
			//Debug.Log("Dropped " + topParent);
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			session.input.RemoveDelegate(inputId, Drop, InputEventType.Press);
			Vector3 newParentRelativePosition = Vector3.zero;
			Quaternion newParentRelativeRotation = Quaternion.identity;
			if (t)
            {
				newParentRelativeRotation = t.rotation;
				newParentRelativePosition=t.position;
			}
			Quaternion dropRelativeRotation = Quaternion.Inverse(newParentRelativeRotation) * dropGrabbableHere.transform.rotation;
			Vector3 dropRelativePosition = Quaternion.Inverse(newParentRelativeRotation) * (dropGrabbableHere.transform.position - newParentRelativePosition);
			teleport.Monitor.Instance.ReparentNode(topParent, (t?t.gameObject:null), dropRelativePosition,dropRelativeRotation, false);
			holderClient=0;
		}
		DropGrabbable dropGrabbableHere= null;
		void OnHoldingTriggerEnter(Collider other)
		{
			var dropGrabbable= other.GetComponent<DropGrabbable>();
			if (dropGrabbable==null)
				return;
			//Debug.Log("Detected collision between " + gameObject.name + " and " + other.name);
			Teleport_SessionComponent session = nearController.session;
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, true);
			var ids = GetAppropriateInputs(nearController);
			if (ids != null)
				session.input.AddDelegate(ids, Drop, InputEventType.Press);
			session.input.AddDelegate(LeftMouseClickInputIds, Drop, InputEventType.Press);
			dropGrabbableHere= dropGrabbable;
		}
		void OnHoldingTriggerExit(Collider other)
		{
			var dropGrabbable = other.GetComponent<DropGrabbable>();
			if (dropGrabbable != dropGrabbableHere)
				return;
			Teleport_SessionComponent session = nearController.session;
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			dropGrabbableHere =null;
		}
		InputID[] GetAppropriateInputs(Teleport_Controller controller)
        {
             if (controller.poseRegexPath.Contains("left"))
             {
                 return LeftGrabInputIds;
             }
             if (controller.poseRegexPath.Contains("right"))
             {
				return RightGrabInputIds;
			}
			 else return null;
		}
         // This occurs when a collider impinges on the Grabbable.
        private void OnTriggerEnter(Collider other)
		{
			if (holderClient != 0)
            {
				OnHoldingTriggerEnter(other);
				return;
			}
			Teleport_Controller controller= other.GetComponentInChildren<Teleport_Controller>();
			if(!controller)
				return;
			if (nearController!=null)
				return;
			//Debug.Log("Detected collision between " + gameObject.name + " and " + other.name);
			Teleport_SessionComponent session= controller.session;
			if (session!=null&&session.GeometryStreamingService!=null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, true);
			var ids= GetAppropriateInputs(controller);
			if(ids!=null)
				session.input.AddDelegate(ids, Grab, InputEventType.Press);
			session.input.AddDelegate(LeftMouseClickInputIds, Grab, InputEventType.Press);
			nearController =controller;
		}
		void OnTriggerStay(Collider other)
		{
		//	Debug.Log(gameObject.name + " and " + other.name + " are still colliding");
		}

		void OnTriggerExit(Collider other)
		{
			if (holderClient != 0)
			{
				OnHoldingTriggerExit(other);
				return;
			}
			Teleport_Controller controller = other.GetComponentInChildren<Teleport_Controller>();
			if (!controller||controller!=nearController)
				return;
			nearController=null;
            //Debug.Log(gameObject.name + " and " + other.name + " are no longer colliding");
			Teleport_SessionComponent session = controller.session;
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			var ids = GetAppropriateInputs(controller);
			if(ids!=null)
				session.input.RemoveDelegate(ids, Grab, InputEventType.Press);
			session.input.RemoveDelegate(LeftMouseClickInputIds, Grab, InputEventType.Press);
		}
	}
}