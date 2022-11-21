using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	using InputID = System.UInt16;
	/// <summary>
	/// An example component to create a snowball when the user activates the Squeeze, or Grip control within its collision area.
	/// </summary>
	public class Snowball : MonoBehaviour
	{
		// Start is called before the first frame update
		void Start()
		{
			TeleportSettings settings=TeleportSettings.GetOrCreateSettings();
		}
		//Vector3 oldRelativePosition=new Vector3();
		//Quaternion oldRelativeRotation=new Quaternion();
		//Teleport_Controller nearController = null;

		public void ThrowSnowball(Input input, InputID inputId)
		{
			GameObject currentParent= gameObject.transform.parent.gameObject;
			Teleport_Streamable currentParentStreamable = currentParent.GetComponent<Teleport_Streamable>();
			Debug.Log("Threw " + gameObject);
			Teleport_SessionComponent session = input.gameObject.GetComponent<Teleport_SessionComponent>();
			session.input.RemoveDelegate(inputId, ThrowSnowball, InputEventType.Release);
			Teleport_Streamable streamable = gameObject.GetComponent<Teleport_Streamable>();

			// We receive velocities in stage space, i.e. in the space relative to the clientspace Root.
			var v = currentParentStreamable.stageSpaceVelocity;
			var a = currentParentStreamable.stageSpaceAngularVelocity;
			Teleport_ClientspaceRoot stageSpace=input.gameObject.GetComponentInParent<Teleport_ClientspaceRoot>();
			if (stageSpace)
			{
				v=stageSpace.transform.TransformVector(v);
				a = stageSpace.transform.TransformVector(a);
			}
			// New parent is "none" - so need to get the global position.
			teleport.Monitor.Instance.ReparentNode(gameObject, null, gameObject.transform.position, gameObject.transform.rotation,true);
			// Activate the object's physics.
			var rigidBody = gameObject.GetComponent<Rigidbody>();
			if (rigidBody)
			{
				rigidBody.isKinematic = false;
				rigidBody.detectCollisions = true;
				rigidBody.velocity= v;
				rigidBody.angularVelocity = a;
			}
			var collider = gameObject.GetComponent<SphereCollider>();
			if(collider)
				collider.isTrigger=false;
		}
	}
}