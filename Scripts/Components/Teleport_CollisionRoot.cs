using UnityEngine;

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class Teleport_CollisionRoot : MonoBehaviour
	{ 
		//Upon collision with another GameObject, 
		private void OnTriggerEnter(Collider other)
		{
			Portal portal=other.GetComponent<teleport.Portal>();
			if(portal!=null)
			{

			}
		}
	}
}
