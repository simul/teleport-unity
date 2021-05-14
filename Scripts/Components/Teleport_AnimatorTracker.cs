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

		public void SetPlayingAnimation(AnimationClip playingClip)
		{
			uid animationID = GeometrySource.GetGeometrySource().FindResourceID(playingClip);
			if(animationID == 0)
			{
				Debug.LogError($"Teleport: Animation update failure! Failed to update playing animation, as \"{playingClip.name}\" has not been extracted!");
				return;
			}

			avs.NodeUpdateAnimation animationUpdate = new avs.NodeUpdateAnimation();
			animationUpdate.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			animationUpdate.nodeID = GeometrySource.GetGeometrySource().FindResourceID(skinnedMeshRenderer.gameObject);
			animationUpdate.animationID = animationID;

			foreach(Teleport_SessionComponent session in hierarchyRoot.GetActiveSessions())
			{
				Client_UpdateNodeAnimation(session.GetClientID(), animationUpdate);
			}
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
