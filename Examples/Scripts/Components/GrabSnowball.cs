using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	using InputID = System.UInt16;
	/// <summary>
	/// An example component to create a snowball when the user activates the Squeeze, or Grip control within its collision area.
	/// </summary>
	public class GrabSnowball : MonoBehaviour
	{
		GameObject topParent=null;
		public GameObject snowballPrefab;
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
			RightGrabToggleInputIds = settings.FindInputsByName("Right Grab Toggle");
			if(snowballPrefab==null)
				snowballPrefab = (GameObject)Resources.Load("Prefabs/Snowball", typeof(GameObject));
		}
		System.UInt16 [] LeftGrabInputIds ={ };
		System.UInt16 [] RightGrabInputIds = { };
		System.UInt16 [] RightGrabToggleInputIds = { };
		//Vector3 oldRelativePosition=new Vector3();
		//Quaternion oldRelativeRotation=new Quaternion();
		Teleport_Controller nearController = null;

		public void GrabASnowball(Input input, InputID inputId)
		{
			Debug.Log("Grab A Snowball");
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			uid holderClient = session.GetClientID();
			if(holderClient==0)
				return;
			// For now, we do the reparenting only client-side on that one client.
			// I expect this to change so that all clients + server recognize the new structure.

			// We will do the following:
			//			* Spawn a snowball object.
			//			* Parent the snowball to the controller.
			//			*   deactivat the GrabASnowball delegate for this input.
			//			* Create a "Throw" delegate for this snowball to be dropped or thrown.

			// Spawn a snowball:

			GameObject snowball = Instantiate<GameObject>(snowballPrefab,  nearController.gameObject.transform);
			Debug.Log("Grabbed " + snowball + " with " + nearController.gameObject);
			var rigidBody= snowball.GetComponent<Rigidbody>();
			if(rigidBody)
			{
				rigidBody.isKinematic = true;
				//rigidBody.detectCollisions = false;
			}
			
			session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			session.input.RemoveDelegate(inputId, GrabASnowball, InputEventType.Release); //InputEventType.Press);
			session.input.RemoveDelegate(inputId, GrabASnowball, InputEventType.Press);
			session.input.AddDelegate(inputId, snowball.GetComponent<Snowball>().ThrowSnowball, InputEventType.Release);
		}
		// This occurs when a collider impinges on the Grabbable.
		private void OnTriggerEnter(Collider other)
		{
			Teleport_Controller controller= other.GetComponentInChildren<Teleport_Controller>();
			if(!controller)
				return;
			if (nearController!=null)
				return;
			Teleport_SessionComponent session= controller.GetComponentInParent<Teleport_SessionComponent>();
			if (session!=null&&session.GeometryStreamingService!=null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, true);
			if(controller.poseRegexPath.Contains("left"))
			{
				session.input.AddDelegate(LeftGrabInputIds, GrabASnowball, InputEventType.Press);
				session.input.AddDelegate(RightGrabToggleInputIds, GrabASnowball, InputEventType.Release); //InputEventType.Press);
			}
			if (controller.poseRegexPath.Contains("right"))
			{ 
				session.input.AddDelegate(RightGrabInputIds, GrabASnowball, InputEventType.Press);
				session.input.AddDelegate(RightGrabToggleInputIds, GrabASnowball, InputEventType.Release); //InputEventType.Press);
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
			Teleport_SessionComponent session = controller.GetComponentInParent<Teleport_SessionComponent>();
			if (session != null && session.GeometryStreamingService != null)
				session.GeometryStreamingService.SetNodeHighlighted(topParent, false);
			if (controller.poseRegexPath.Contains("left"))
			{ 
				session.input.RemoveDelegate(LeftGrabInputIds, GrabASnowball, InputEventType.Press);
				session.input.RemoveDelegate(RightGrabToggleInputIds, GrabASnowball, InputEventType.Release); //InputEventType.Press);
			}
			if (controller.poseRegexPath.Contains("right"))
			{
				session.input.RemoveDelegate(RightGrabInputIds, GrabASnowball, InputEventType.Press);
				session.input.RemoveDelegate(RightGrabToggleInputIds, GrabASnowball, InputEventType.Release); //InputEventType.Press);
			}
		}

	}
}