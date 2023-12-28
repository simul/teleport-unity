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
	public class Throwable : MonoBehaviour
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
			LeftGrabInputIds = settings.FindInputsByName("Left Grab");
			RightGrabInputIds = settings.FindInputsByName("Right Grab");
			LeftGrabToggleInputIds = settings.FindInputsByName("Left Grab Toggle");  
			RightGrabToggleInputIds = settings.FindInputsByName("Right Grab Toggle"); 
		}

		// Update is called once per frame
		void Update()
		{
		
		}
		public uid holderClient=0;
		System.UInt16 [] LeftGrabInputIds ={ };
		System.UInt16 [] RightGrabInputIds = { };
		System.UInt16 [] LeftGrabToggleInputIds = { };
		System.UInt16[] RightGrabToggleInputIds = { };
		GameObject formerParent=null;
		Vector3 oldRelativePosition=new Vector3();
		Quaternion oldRelativeRotation=new Quaternion();
		Teleport_Controller nearController = null;

		public void Grab(Input input, InputID inputId)
		{
			Debug.Log("Grab");
			if (holderClient != 0)
				return;
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			holderClient = session.GetClientID();
			if(holderClient==0)
				return;
			Debug.Log("Grabbed " + topParent + " with " + nearController.gameObject);
			var rigidBody= topParent.GetComponent<Rigidbody>();
			if(rigidBody)
			{
				rigidBody.isKinematic = true;
				//rigidBody.detectCollisions = false;
			}
			// For now, we do the reparenting only client-side on that one client.
			// I expect this to change so that all clients + server recognize the new structure.
			formerParent = topParent.transform.parent?topParent.transform.parent.gameObject:null;
			teleport.Monitor casterMonitor = teleport.Monitor.Instance;
			oldRelativePosition= topParent.transform.localPosition;
			oldRelativeRotation= topParent.transform.localRotation;
			Transform grabbableT	= gameObject.transform;
			Transform childT		= topParent.transform;
			Quaternion relativeRotation = Quaternion.Inverse(grabbableT.rotation) * childT.rotation;
			Vector3 relativePosition= Quaternion.Inverse(grabbableT.rotation) *(childT.position- grabbableT.position);
			teleport.Monitor.Instance.ReparentNode(topParent, nearController.gameObject, relativePosition, relativeRotation, true);
			
			session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			session.input.RemoveDelegate(inputId, Grab, InputEventType.Release); //InputEventType.Press);
			session.input.RemoveDelegate(inputId, Grab, InputEventType.Press);
			session.input.AddDelegate(inputId, Drop, InputEventType.Release);
		}
		public void Drop(Input input, InputID inputId)
		{
			if (holderClient == 0)
				return;
			GameObject currentParent= topParent.transform.parent.gameObject;
			teleport.StreamableRoot currentParentStreamable = currentParent.GetComponentInParent<teleport.StreamableRoot>();
			Debug.Log("Dropped " + topParent);
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			session.input.RemoveDelegate(inputId, Drop, InputEventType.Release);
			teleport.StreamableRoot streamable = topParent.GetComponent<teleport.StreamableRoot>();

			teleport.StreamableNode streamedNode = currentParentStreamable.GetStreamedNode(currentParent);
			// We receive velocities in stage space, i.e. in the space relative to the clientspace Root.
			var v = streamedNode.stageSpaceVelocity;
			var a = streamedNode.stageSpaceAngularVelocity;
			Teleport_ClientspaceRoot stageSpace=input.gameObject.GetComponentInParent<Teleport_ClientspaceRoot>();
			if (stageSpace)
			{
				v=stageSpace.transform.TransformVector(v);
				a = stageSpace.transform.TransformVector(a);
			}
			// New parent is "none" - so need to get the global position.
			teleport.Monitor.Instance.ReparentNode(topParent, null, topParent.transform.position, topParent.transform.rotation,true);
			// Activate the object's physics.
			var rigidBody = topParent.GetComponent<Rigidbody>();
			if (rigidBody)
			{
				rigidBody.isKinematic = false;
				rigidBody.detectCollisions = true;
				rigidBody.velocity= v;
				rigidBody.angularVelocity = a;
			}
			var collider = topParent.GetComponent<SphereCollider>();
			if(collider)
				collider.isTrigger=false;
			holderClient =0;
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
			Teleport_SessionComponent session= controller.GetComponentInParent<Teleport_SessionComponent>();
			if (session!=null&&session.GeometryStreamingService!=null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, true);
			if(controller.poseRegexPath.Contains("left"))
			{
				session.input.AddDelegate(LeftGrabInputIds, Grab, InputEventType.Press);
				session.input.AddDelegate(RightGrabToggleInputIds, Grab, InputEventType.Release); //InputEventType.Press);
			}
			if (controller.poseRegexPath.Contains("right"))
			{ 
				session.input.AddDelegate(RightGrabInputIds, Grab, InputEventType.Press);
				session.input.AddDelegate(RightGrabToggleInputIds, Grab, InputEventType.Release); //InputEventType.Press);
			}
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
			Teleport_SessionComponent session = controller.GetComponentInParent<Teleport_SessionComponent>();
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			if (controller.poseRegexPath.Contains("left"))
			{ 
				session.input.RemoveDelegate(LeftGrabInputIds, Grab, InputEventType.Press);
				session.input.RemoveDelegate(RightGrabToggleInputIds, Grab, InputEventType.Release); //InputEventType.Press);
			}
			if (controller.poseRegexPath.Contains("right"))
			{
				session.input.RemoveDelegate(RightGrabInputIds, Grab, InputEventType.Press);
				session.input.RemoveDelegate(RightGrabToggleInputIds, Grab, InputEventType.Release); //InputEventType.Press);
			}
		}

	}
}