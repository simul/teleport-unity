using System;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	[DisallowMultipleComponent]
	public class Teleport_AnimatorTracker : MonoBehaviour
	{
		#region DllImport

		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateNodeAnimation(uid clientID, avs.NodeUpdateAnimation update);

		#endregion //DllImport

		//Teleport_Streamable that is the root of the hierarchy this GameObject with an Animator component is on.
		public Teleport_Streamable hierarchyRoot;

		private SkinnedMeshRenderer skinnedMeshRenderer;

		//Animation update that last occurred.
		private avs.NodeUpdateAnimation lastAnimationUpdate;

		//Sets the streamed playing animation on this node.
		//Called by an AnimationEvent added to all extracted animations while in play-mode.
		public void SetPlayingAnimation(AnimationClip playingClip)
		{
			uid animationID = GeometrySource.GetGeometrySource().FindResourceID(playingClip);
			if(animationID == 0)
			{
				Debug.LogError($"Teleport: Animation update failure! Failed to update playing animation, as \"{playingClip.name}\" has not been extracted!");
				return;
			}

			lastAnimationUpdate = new avs.NodeUpdateAnimation();
			lastAnimationUpdate.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			lastAnimationUpdate.nodeID = GeometrySource.GetGeometrySource().FindResourceID(skinnedMeshRenderer.gameObject);
			lastAnimationUpdate.animationID = animationID;

			foreach(Teleport_SessionComponent session in hierarchyRoot.GetActiveSessions())
			{
				SendPlayingAnimation(session.GetClientID());
			}
		}

		//Sends the currently playing animation to the client with the passed ID.
		public void SendPlayingAnimation(uid clientID)
		{
			Client_UpdateNodeAnimation(clientID, lastAnimationUpdate);
		}

		private void Awake()
		{
			skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
			if(!skinnedMeshRenderer)
			{
				Debug.LogWarning($"{nameof(Teleport_AnimatorTracker)} was added to GameObject \"{name}\", but there was no SkinnedMeshRenderer in the hierarchy of this GameObject. Deleting.");
				Destroy(this);
			}
		}
	}
}
